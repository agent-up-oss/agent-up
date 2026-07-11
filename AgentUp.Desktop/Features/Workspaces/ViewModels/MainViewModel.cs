using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Workspaces.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private readonly WorkspaceApiClient _client;

    private WorkspaceItemViewModel? _selectedWorkspace;
    private bool _isSidebarCollapsed;
    private bool _isLoading;
    private string? _errorMessage;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspaceItemViewModel? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set => this.RaiseAndSetIfChanged(ref _selectedWorkspace, value);
    }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (_isSidebarCollapsed == value) return;
            _isSidebarCollapsed = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsSidebarExpanded));
            this.RaisePropertyChanged(nameof(SidebarWidth));
            this.RaisePropertyChanged(nameof(SidebarToggleIcon));
        }
    }

    public bool IsSidebarExpanded => !_isSidebarCollapsed;
    public double SidebarWidth => _isSidebarCollapsed ? 56 : 220;
    public string SidebarToggleIcon => _isSidebarCollapsed ? "›" : "‹";

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSidebarCommand { get; }

    public MainViewModel(WorkspaceApiClient client)
    {
        _client = client;
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadWorkspacesAsync);
        ToggleSidebarCommand = ReactiveCommand.Create(() => { IsSidebarCollapsed = !IsSidebarCollapsed; });
    }

    public async Task InitializeAsync() => await LoadWorkspacesAsync();

    private async Task LoadWorkspacesAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var workspaces = await _client.ListAsync(ct);
            Workspaces.Clear();
            foreach (var ws in workspaces)
                Workspaces.Add(new WorkspaceItemViewModel(
                    ws.Id, ws.DisplayName, ws.Branch, ws.RepositoryPath, ws.WorktreePath, ws.State));

            if (SelectedWorkspace is null && Workspaces.Count > 0)
                SelectedWorkspace = Workspaces[0];
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
