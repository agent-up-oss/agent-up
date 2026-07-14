using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Workspaces.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceListViewModel : ReactiveObject
{
    private readonly WorkspaceApiClient _client;
    private WorkspaceItemViewModel? _selectedWorkspace;
    private bool _isCollapsed;
    private bool _isLoading;
    private string? _errorMessage;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspaceItemViewModel? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedWorkspace, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsExpanded));
            this.RaisePropertyChanged(nameof(Width));
            this.RaisePropertyChanged(nameof(ToggleIcon));
        }
    }

    public bool IsExpanded => !_isCollapsed;
    public double Width => _isCollapsed ? 56 : 220;
    public string ToggleIcon => _isCollapsed ? "›" : "‹";

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool ShowEmptyState => _selectedWorkspace is null && _errorMessage is null && !_isLoading;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

    public WorkspaceListViewModel(WorkspaceApiClient client)
    {
        _client = client;
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsCollapsed = !IsCollapsed; });
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var selectedId = SelectedWorkspace?.Id;
            var workspaces = await _client.ListAsync(ct);
            Workspaces.Clear();
            foreach (var ws in workspaces)
                Workspaces.Add(new WorkspaceItemViewModel(
                    ws.Id, ws.DisplayName, ws.Branch, ws.RepositoryPath, ws.WorktreePath, ws.State,
                    ws.Applications));

            SelectedWorkspace = selectedId is null
                ? Workspaces.FirstOrDefault()
                : Workspaces.FirstOrDefault(ws => ws.Id == selectedId) ?? Workspaces.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not connect to Agent-Up Server: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
