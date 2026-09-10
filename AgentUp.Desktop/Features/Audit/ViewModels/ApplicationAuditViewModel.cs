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

    private readonly ApplicationAuditController _audit;
    private readonly ApplicationAuditStreamClient? _stream;
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

    private readonly Stack<(DateTimeOffset? Before, string? BeforeEventId)> _pageCursors = new();
    private (DateTimeOffset? Before, string? BeforeEventId)? _nextPageCursor;
    private string? _workspaceId;
    private string? _application;
    private CancellationTokenSource? _activeLoad;
    private CancellationTokenSource? _streamCts;
    private long _loadVersion;
    private bool _isLoading;
    private bool _isStreaming = true;

    public IReadOnlyList<DiagnosticKindFilterOption> KindFilters => _kindFilters;
    public ObservableCollection<ApplicationAuditEventViewModel> Events { get; } = [];

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isStreaming, value);
            this.RaisePropertyChanged(nameof(StreamingButtonText));
            this.RaisePropertyChanged(nameof(CanRefresh));
        }
    }

    public string StreamingButtonText => IsStreaming ? "Streaming Live" : "Stopped Streaming";

    public bool CanRefresh => !IsStreaming && !IsLoading;

    public bool CanGoPrevious => _pageCursors.Count > 1;

    public bool CanGoNext => _nextPageCursor is not null;

    public string PageLabel => $"Page {_pageCursors.Count}";

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
        => $"No diagnostic entries in these categories: [{string.Join(", ", SelectedCategoryLabels())}]";

    public ReactiveCommand<Unit, Unit> ToggleStreamingCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }

    public ApplicationAuditViewModel(ApplicationAuditController audit, ApplicationAuditStreamClient? stream = null)
    {
        _audit = audit;
        _stream = stream;

        var canGoPrevious = this.WhenAnyValue(x => x.CanGoPrevious);
        var canGoNext = this.WhenAnyValue(x => x.CanGoNext);
        var canRefresh = this.WhenAnyValue(x => x.CanRefresh);

        ToggleStreamingCommand = ReactiveCommand.Create(ToggleStreaming);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
        PreviousPageCommand = ReactiveCommand.CreateFromTask(GoPreviousAsync, canGoPrevious);
        NextPageCommand = ReactiveCommand.CreateFromTask(GoNextAsync, canGoNext);

        foreach (var filter in _kindFilters)
            filter.WhenAnyValue(option => option.IsSelected)
                .Skip(1)
                .Subscribe(_ => OnKindSelectionChanged());
    }

    public async Task LoadAsync(string workspaceId, string application, CancellationToken cancellationToken = default)
    {
        StopStream();
        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _application = application;
        ResetPagination();
        Events.Clear();
        RaiseEmptyStateProperties();
        IsLoading = false;
        try
        {
            await LoadCurrentPageAsync(version, load.Token);
            if (IsStreaming)
                StartStream();
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

    private void ToggleStreaming()
    {
        IsStreaming = !IsStreaming;
        if (IsStreaming)
        {
            StartStream();
            return;
        }

        StopStream();
    }

    private Task RefreshAsync()
        => _workspaceId is null || _application is null || IsStreaming
            ? Task.CompletedTask
            : LoadCurrentPageAsync(_loadVersion, CancellationToken.None);

    private Task GoPreviousAsync()
    {
        if (!CanGoPrevious)
            return Task.CompletedTask;

        _pageCursors.Pop();
        RaisePaginationProperties();
        return LoadCurrentPageAsync(_loadVersion, CancellationToken.None);
    }

    private Task GoNextAsync()
    {
        if (_nextPageCursor is not { } next)
            return Task.CompletedTask;

        _pageCursors.Push(next);
        RaisePaginationProperties();
        return LoadCurrentPageAsync(_loadVersion, CancellationToken.None);
    }

    private void OnKindSelectionChanged()
    {
        RaiseEmptyStateProperties();
        if (_workspaceId is null || _application is null)
            return;

        _ = LoadAsync(_workspaceId, _application);
    }

    private void ResetPagination()
    {
        _pageCursors.Clear();
        _pageCursors.Push((null, null));
        _nextPageCursor = null;
        RaisePaginationProperties();
    }

    private IReadOnlyList<string> SelectedKinds()
        => _kindFilters
            .Where(option => option.IsSelected)
            .Select(option => option.Kind)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IReadOnlyList<string> SelectedStreams()
        => _kindFilters
            .Where(option => option.IsSelected && option.Stream is not null)
            .Select(option => option.Stream!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IEnumerable<string> SelectedCategoryLabels()
        => _kindFilters.Where(option => option.IsSelected).Select(option => option.Label);

    private async Task LoadCurrentPageAsync(long version, CancellationToken cancellationToken)
    {
        if (_workspaceId is null || _application is null || IsLoading)
            return;

        var workspaceId = _workspaceId;
        var application = _application;
        var cursor = _pageCursors.Peek();
        var kinds = SelectedKinds();
        var streams = SelectedStreams();
        if (kinds.Count == 0)
        {
            if (version != _loadVersion)
                return;

            Events.Clear();
            _nextPageCursor = null;
            RaisePaginationProperties();
            RaiseEmptyStateProperties();
            return;
        }

        IsLoading = true;
        try
        {
            var page = await _audit.GetPageAsync(
                workspaceId,
                application,
                kinds,
                streams,
                cursor.Before,
                cursor.BeforeEventId,
                PageSize,
                cancellationToken);
            if (version != _loadVersion
                || !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
                || !string.Equals(application, _application, StringComparison.Ordinal))
                return;

            Events.Clear();
            foreach (var item in page.Items)
                Events.Add(new ApplicationAuditEventViewModel(item));

            _nextPageCursor = page.NextBefore is null
                ? null
                : (page.NextBefore, page.NextBeforeEventId);
            RaisePaginationProperties();
            RaiseEmptyStateProperties();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Superseded audit page load was cancelled.");
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

    private void StartStream()
    {
        if (_stream is null || _workspaceId is null || _application is null || !IsStreaming)
            return;

        StopStream();
        _streamCts = new CancellationTokenSource();
        var workspaceId = _workspaceId;
        var application = _application;
        var kinds = SelectedKinds();
        var streams = SelectedStreams();
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
        if (!IsStreaming || _pageCursors.Count > 1)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!IsStreaming || _pageCursors.Count > 1)
                return;

            if (Events.Any(item => string.Equals(item.EventId, dto.EventId, StringComparison.Ordinal)))
                return;

            Events.Insert(0, new ApplicationAuditEventViewModel(dto));
            while (Events.Count > PageSize)
                Events.RemoveAt(Events.Count - 1);

            RaiseEmptyStateProperties();
        });
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
        this.RaisePropertyChanged(nameof(CanGoPrevious));
        this.RaisePropertyChanged(nameof(CanGoNext));
        this.RaisePropertyChanged(nameof(PageLabel));
    }

    private void RaiseEmptyStateProperties()
    {
        this.RaisePropertyChanged(nameof(ShowEmptyState));
        this.RaisePropertyChanged(nameof(EmptyMessage));
    }
}
