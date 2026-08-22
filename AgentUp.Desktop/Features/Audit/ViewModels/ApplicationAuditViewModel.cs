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
    private string? _workspaceId;
    private string? _application;
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
        _workspaceId = workspaceId;
        _application = application;
        _nextBefore = null;
        Events.Clear();
        await LoadPageAsync(cancellationToken);
    }

    private Task LoadMoreAsync() => LoadPageAsync(CancellationToken.None);

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        if (_workspaceId is null || _application is null || IsLoading) return;
        IsLoading = true;
        try
        {
            var page = await _audit.GetPageAsync(_workspaceId, _application, _nextBefore, PageSize, cancellationToken);
            foreach (var item in page.Items) Events.Add(new ApplicationAuditEventViewModel(item));
            _nextBefore = page.NextBefore;
            this.RaisePropertyChanged(nameof(HasMore));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
