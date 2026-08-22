using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive;
using AgentUp.Desktop.Features.Audit.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class ApplicationAuditViewModel : ReactiveObject
{
    internal const int PageSize = 50;
    private readonly ApplicationAuditController _audit;
    private DateTimeOffset? _nextBefore;
    private string? _nextBeforeEventId;
    private string? _workspaceId;
    private string? _application;
    private CancellationTokenSource? _activeLoad;
    private long _loadVersion;
    private bool _isLoading;

    public ObservableCollection<ApplicationAuditEventViewModel> Events { get; } = [];
    public bool HasMore => _nextBefore is not null;
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }
    public ReactiveCommand<Unit, Unit> LoadMoreCommand { get; }

    public ApplicationAuditViewModel(ApplicationAuditController audit)
    {
        _audit = audit;
        LoadMoreCommand = ReactiveCommand.CreateFromTask(LoadMoreAsync);
    }

    public async Task LoadAsync(string workspaceId, string application, CancellationToken cancellationToken = default)
    {
        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _application = application;
        _nextBefore = null;
        _nextBeforeEventId = null;
        Events.Clear();
        IsLoading = false;
        try
        {
            await LoadPageAsync(version, workspaceId, application, load.Token);
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, load)) _activeLoad = null;
        }
    }

    private Task LoadMoreAsync()
        => _workspaceId is null || _application is null
            ? Task.CompletedTask
            : LoadPageAsync(_loadVersion, _workspaceId, _application, CancellationToken.None);

    private async Task LoadPageAsync(long version, string workspaceId, string application, CancellationToken cancellationToken)
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var page = await _audit.GetPageAsync(
                workspaceId, application, _nextBefore, _nextBeforeEventId, PageSize, cancellationToken);
            if (version != _loadVersion
                || !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
                || !string.Equals(application, _application, StringComparison.Ordinal)) return;
            foreach (var item in page.Items) Events.Add(new ApplicationAuditEventViewModel(item));
            _nextBefore = page.NextBefore;
            _nextBeforeEventId = page.NextBeforeEventId;
            this.RaisePropertyChanged(nameof(HasMore));
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
            if (version == _loadVersion) IsLoading = false;
        }
    }
}
