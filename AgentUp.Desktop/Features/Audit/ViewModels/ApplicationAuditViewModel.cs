using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Audit.Controllers;
using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Providers;
using Avalonia.Threading;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class ApplicationAuditViewModel : ReactiveObject
{
    internal const int PageSize = 50;
    internal const int PageRadius = 5;
    internal const int FetchPageSize = 100;
    internal const int MaxWindowFetchPages = 5;

    private readonly ApplicationAuditController _audit;
    private readonly ApplicationAuditStreamClient? _stream;
    private readonly List<ApplicationAuditEventDto> _sourceEvents = [];
    private readonly IReadOnlyList<DiagnosticKindFilterOption> _kindFilters =
    [
        new("Frontend", "frontend"),
        new("Stdout", "application", isSelected: false, stream: "stdout"),
        new("Stderr", "application", isSelected: false, stream: "stderr"),
        new("Health", "health"),
        new("Metrics", "metrics"),
        new("Browser", "browser"),
        new("Workspace", "workspace"),
        new("Stream", "stream"),
    ];

    private int _totalPages = 1;
    private int _windowStartPage = 1;
    private int _windowEndPage = 1;
    private bool _serverHasMore;
    private string? _workspaceId;
    private string? _application;
    private CancellationTokenSource? _activeLoad;
    private CancellationTokenSource? _streamCts;
    private long _loadVersion;
    private bool _isLoading;
    private bool _isStreaming = true;
    private int _currentPage = 1;
    private CancellationTokenSource? _filterChangeDebounce;
    private CancellationTokenSource? _streamViewDebounce;
    private string _searchText = string.Empty;

    public IReadOnlyList<DiagnosticKindFilterOption> KindFilters => _kindFilters;
    public ObservableCollection<ApplicationAuditEventViewModel> Events { get; } = [];
    public ObservableCollection<DiagnosticPageJumpViewModel> PageJumpButtons { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (string.Equals(_searchText, value, StringComparison.Ordinal))
                return;

            this.RaiseAndSetIfChanged(ref _searchText, value);
            ResetToFirstPage();
            _ = ReloadWindowAsync(1, keepStream: true);
        }
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isStreaming, value);
            this.RaisePropertyChanged(nameof(StreamingButtonText));
        }
    }

    public string StreamingButtonText => IsStreaming ? "Streaming Live" : "Stopped Streaming";

    public bool CanRefresh => !IsLoading;

    public int CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public bool CanGoFirst => CurrentPage > 1;

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext => CurrentPage < _totalPages;

    public bool CanGoLast => CurrentPage < _totalPages;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
            this.RaisePropertyChanged(nameof(CanRefresh));
        }
    }

    public bool ShowEmptyState => !IsLoading && Events.Count == 0;

    public string EmptyMessage
    {
        get
        {
            if (!SelectedKindFilters().Any())
                return $"No diagnostic entries in these categories: [{string.Join(", ", SelectedCategoryLabels())}]";

            if (!string.IsNullOrWhiteSpace(SearchText))
                return $"No diagnostic entries matching \"{SearchText.Trim()}\".";

            return "No diagnostic entries match the current filters.";
        }
    }

    public ReactiveCommand<Unit, Unit> ToggleStreamingCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> FirstPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LastPageCommand { get; }

    public ApplicationAuditViewModel(ApplicationAuditController audit, ApplicationAuditStreamClient? stream = null)
    {
        _audit = audit;
        _stream = stream;

        var canGoFirst = this.WhenAnyValue(x => x.CanGoFirst);
        var canGoPrevious = this.WhenAnyValue(x => x.CanGoPrevious);
        var canGoNext = this.WhenAnyValue(x => x.CanGoNext);
        var canGoLast = this.WhenAnyValue(x => x.CanGoLast);
        var canRefresh = this.WhenAnyValue(x => x.CanRefresh);

        ToggleStreamingCommand = ReactiveCommand.CreateFromTask(ToggleStreamingAsync);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
        FirstPageCommand = ReactiveCommand.CreateFromTask(GoFirstAsync, canGoFirst);
        PreviousPageCommand = ReactiveCommand.CreateFromTask(GoPreviousAsync, canGoPrevious);
        NextPageCommand = ReactiveCommand.CreateFromTask(GoNextAsync, canGoNext);
        LastPageCommand = ReactiveCommand.CreateFromTask(GoLastAsync, canGoLast);

        Observable.Merge(_kindFilters.Select(filter => filter.WhenAnyValue(option => option.IsSelected).Skip(1)))
            .Subscribe(_ => ScheduleKindSelectionReload());
    }

    public async Task LoadAsync(string workspaceId, string application, CancellationToken cancellationToken = default)
    {
        var sameTarget = string.Equals(_workspaceId, workspaceId, StringComparison.Ordinal)
            && string.Equals(_application, application, StringComparison.Ordinal);
        if (sameTarget && _sourceEvents.Count > 0)
        {
            if (IsStreaming)
                StartStream();
            ApplyView();
            return;
        }

        StopStream();
        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _application = application;
        ResetToFirstPage();
        _sourceEvents.Clear();
        ApplyView();
        try
        {
            if (IsStreaming)
                StartStream();

            if (SelectedKindFilters().Any())
                await LoadWindowAsync(1, version, load.Token, fetchToEnd: false);
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, load))
                _activeLoad = null;
        }
    }

    public void Deactivate()
    {
        StopStream();
        _activeLoad?.Cancel();
        _activeLoad = null;
    }

    private async Task ToggleStreamingAsync()
    {
        IsStreaming = !IsStreaming;
        await ReloadWindowAsync(1, keepStream: false);
    }

    private Task RefreshAsync()
        => _workspaceId is null || _application is null
            ? Task.CompletedTask
            : ReloadWindowAsync(CurrentPage, keepStream: true);

    private async Task ReloadWindowAsync(int targetPage, bool keepStream)
    {
        if (_workspaceId is null || _application is null)
            return;

        if (!keepStream)
            StopStream();

        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = new CancellationTokenSource();
        _activeLoad = load;
        try
        {
            if (!SelectedKindFilters().Any())
            {
                _sourceEvents.Clear();
                _serverHasMore = false;
                _totalPages = 1;
                ResetToFirstPage();
                ApplyView();
                return;
            }

            await LoadWindowAsync(targetPage, version, load.Token, fetchToEnd: false);
            if (keepStream && IsStreaming)
                StartStream();
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, load))
                _activeLoad = null;
        }
    }

    private Task GoFirstAsync()
        => GoToPageAsync(1);

    private Task GoPreviousAsync()
        => CurrentPage <= 1 ? Task.CompletedTask : GoToPageAsync(CurrentPage - 1);

    private Task GoNextAsync()
        => CurrentPage >= _totalPages ? Task.CompletedTask : GoToPageAsync(CurrentPage + 1);

    private async Task GoLastAsync()
    {
        if (_serverHasMore)
        {
            _activeLoad?.Cancel();
            var version = ++_loadVersion;
            using var load = new CancellationTokenSource();
            _activeLoad = load;
            try
            {
                await LoadWindowAsync(int.MaxValue, version, load.Token, fetchToEnd: true);
            }
            finally
            {
                if (ReferenceEquals(_activeLoad, load))
                    _activeLoad = null;
            }
        }

        await GoToPageAsync(_totalPages);
    }

    private Task GoToPageAsync(int targetPage)
    {
        if (targetPage < 1)
            return Task.CompletedTask;

        if (targetPage > _totalPages && !_serverHasMore)
            return Task.CompletedTask;

        if (targetPage < _windowStartPage
            || targetPage > _windowEndPage
            || !HasLocalPage(targetPage))
            return ReloadWindowAsync(targetPage, keepStream: true);

        CurrentPage = Math.Min(targetPage, _totalPages);
        ApplyView();
        return Task.CompletedTask;
    }

    private bool HasLocalPage(int targetPage)
    {
        var localPageIndex = targetPage - _windowStartPage;
        if (localPageIndex < 0)
            return false;

        return _sourceEvents.Count >= (localPageIndex + 1) * PageSize;
    }

    private void ScheduleKindSelectionReload()
    {
        ResetToFirstPage();
        _filterChangeDebounce?.Cancel();
        _filterChangeDebounce?.Dispose();
        _filterChangeDebounce = new CancellationTokenSource();
        var token = _filterChangeDebounce.Token;
        _ = DebouncedKindSelectionReloadAsync(token);
    }

    private async Task DebouncedKindSelectionReloadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(50, cancellationToken);
            await ReloadWindowAsync(1, keepStream: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Superseded audit filter reload was cancelled.");
        }
    }

    private void ResetToFirstPage()
    {
        CurrentPage = 1;
    }

    private IEnumerable<DiagnosticKindFilterOption> SelectedKindFilters()
        => _kindFilters.Where(option => option.IsSelected);

    private IEnumerable<string> SelectedCategoryLabels()
        => SelectedKindFilters().Select(option => option.Label);

    private async Task LoadWindowAsync(
        int targetPage,
        long version,
        CancellationToken cancellationToken,
        bool fetchToEnd)
    {
        if (_workspaceId is null || _application is null)
            return;

        var workspaceId = _workspaceId;
        var application = _application;
        var windowStart = Math.Max(1, targetPage - PageRadius);
        var windowEnd = targetPage + PageRadius;
        var (kinds, streams) = DiagnosticKindFilterQuery.FromSelection(_kindFilters);
        var windowEventCapacity = (windowEnd - windowStart + 1) * PageSize;
        var requiredFilteredCount = fetchToEnd
            ? int.MaxValue
            : Math.Min(windowEventCapacity, (targetPage - windowStart + 1) * PageSize);

        IsLoading = true;
        try
        {
            var rawEvents = new List<ApplicationAuditEventDto>();
            DateTimeOffset? before = null;
            string? beforeEventId = null;
            _serverHasMore = false;
            var fetchPages = 0;

            while (fetchPages < MaxWindowFetchPages)
            {
                fetchPages++;
                var pageLimit = fetchPages == 1 ? PageSize : FetchPageSize;
                var page = await _audit.GetPageAsync(
                    workspaceId,
                    application,
                    kinds,
                    streams,
                    before,
                    beforeEventId,
                    pageLimit,
                    cancellationToken);
                if (version != _loadVersion
                    || !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
                    || !string.Equals(application, _application, StringComparison.Ordinal))
                    return;

                rawEvents.AddRange(page.Items);
                var filteredCount = FilterEvents(rawEvents).Count;
                if (!fetchToEnd && filteredCount >= requiredFilteredCount)
                {
                    _serverHasMore = page.NextBefore is not null;
                    break;
                }

                if (page.NextBefore is null)
                {
                    _serverHasMore = false;
                    break;
                }

                _serverHasMore = true;
                if (page.Items.Count < pageLimit)
                    break;

                before = page.NextBefore;
                beforeEventId = page.NextBeforeEventId;
            }

            if (version != _loadVersion)
                return;

            var filtered = FilterEvents(rawEvents);
            if (fetchToEnd)
            {
                windowStart = Math.Max(1, filtered.Count == 0
                    ? 1
                    : (int)Math.Ceiling(filtered.Count / (double)PageSize) - PageRadius);
                windowEnd = windowStart + (PageRadius * 2);
            }

            var windowStartIndex = (windowStart - 1) * PageSize;
            var windowLength = (windowEnd - windowStart + 1) * PageSize;
            _sourceEvents.Clear();
            _sourceEvents.AddRange(filtered.Skip(windowStartIndex).Take(windowLength));
            _windowStartPage = windowStart;
            _windowEndPage = windowEnd;
            _totalPages = filtered.Count == 0
                ? 1
                : _serverHasMore
                    ? Math.Max(windowEnd + 1, (int)Math.Ceiling(filtered.Count / (double)PageSize))
                    : (int)Math.Ceiling(filtered.Count / (double)PageSize);

            CurrentPage = filtered.Count == 0
                ? 1
                : Math.Clamp(targetPage, 1, _totalPages);
            ApplyView();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Superseded audit refresh was cancelled.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (version == _loadVersion)
                IsLoading = false;
        }
    }

    private List<ApplicationAuditEventDto> FilterEvents(IEnumerable<ApplicationAuditEventDto> events)
        => events
            .Where(dto => DiagnosticEventFilter.MatchesCategories(dto, _kindFilters))
            .Where(dto => DiagnosticEventFilter.MatchesSearch(dto, SearchText))
            .ToList();

    private void ApplyView()
    {
        if (!SelectedKindFilters().Any())
        {
            Events.Clear();
            _totalPages = 1;
            RebuildPageJumpButtons();
            RaisePaginationProperties();
            RaiseEmptyStateProperties();
            return;
        }

        if (CurrentPage > _totalPages)
            CurrentPage = _totalPages;

        Events.Clear();
        var localPageIndex = CurrentPage - _windowStartPage;
        foreach (var item in _sourceEvents.Skip(localPageIndex * PageSize).Take(PageSize))
            Events.Add(new ApplicationAuditEventViewModel(item));

        RebuildPageJumpButtons();
        RaisePaginationProperties();
        RaiseEmptyStateProperties();
    }

    private void RebuildPageJumpButtons()
    {
        PageJumpButtons.Clear();
        var start = Math.Max(1, CurrentPage - 3);
        var end = Math.Min(_totalPages, CurrentPage + 3);
        for (var page = start; page <= end; page++)
        {
            var targetPage = page;
            PageJumpButtons.Add(new DiagnosticPageJumpViewModel(
                targetPage,
                targetPage == CurrentPage,
                ReactiveCommand.CreateFromTask(() => GoToPageAsync(targetPage))));
        }
    }

    private void StartStream()
    {
        if (_stream is null || _workspaceId is null || _application is null || !IsStreaming)
            return;

        StopStream();
        _streamCts = new CancellationTokenSource();
        var workspaceId = _workspaceId;
        var application = _application;
        var (kinds, streams) = DiagnosticKindFilterQuery.FromSelection(_kindFilters);
        var token = _streamCts.Token;
        _ = RunStreamAsync(workspaceId, application, kinds, streams, token);
    }

    private void StopStream()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    private async Task RunStreamAsync(
        string workspaceId,
        string application,
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> streams,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested && IsStreaming)
        {
            try
            {
                await _stream!.StreamAsync(
                    workspaceId,
                    application,
                    kinds,
                    streams,
                    OnStreamEvent,
                    cancellationToken);
                if (!await DelayAsync(delay, cancellationToken))
                    break;
                if (delay < TimeSpan.FromSeconds(30))
                    delay *= 2;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                Trace.TraceWarning($"[ApplicationAuditViewModel] Stream disconnected: {ex.Message}");
                if (!await DelayAsync(delay, cancellationToken))
                    break;
                if (delay < TimeSpan.FromSeconds(30))
                    delay *= 2;
            }
        }
    }

    private void OnStreamEvent(ApplicationAuditEventDto dto)
    {
        if (!IsStreaming)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!IsStreaming || CurrentPage != 1)
                return;

            if (!DiagnosticEventFilter.MatchesCategories(dto, _kindFilters)
                || !DiagnosticEventFilter.MatchesSearch(dto, SearchText))
                return;

            if (_sourceEvents.Any(item => string.Equals(item.EventId, dto.EventId, StringComparison.Ordinal)))
                return;

            _sourceEvents.Insert(0, dto);
            _windowStartPage = 1;
            _windowEndPage = 1 + PageRadius;
            var maxWindowEvents = (_windowEndPage - _windowStartPage + 1) * PageSize;
            if (_sourceEvents.Count > maxWindowEvents)
                _sourceEvents.RemoveRange(maxWindowEvents, _sourceEvents.Count - maxWindowEvents);

            _totalPages = Math.Max(_totalPages, (int)Math.Ceiling(_sourceEvents.Count / (double)PageSize));
            ScheduleStreamViewUpdate();
        });
    }

    private void ScheduleStreamViewUpdate()
    {
        _streamViewDebounce?.Cancel();
        _streamViewDebounce?.Dispose();
        _streamViewDebounce = new CancellationTokenSource();
        var token = _streamViewDebounce.Token;
        _ = DebouncedStreamViewUpdateAsync(token);
    }

    private async Task DebouncedStreamViewUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(100, cancellationToken);
            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested || !IsStreaming || CurrentPage != 1)
                    return;

                ApplyView();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Superseded audit stream view update was cancelled.");
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void RaisePaginationProperties()
    {
        this.RaisePropertyChanged(nameof(CanGoFirst));
        this.RaisePropertyChanged(nameof(CanGoPrevious));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(CanGoLast));
    }

    private void RaiseEmptyStateProperties()
    {
        this.RaisePropertyChanged(nameof(ShowEmptyState));
        this.RaisePropertyChanged(nameof(EmptyMessage));
    }
}
