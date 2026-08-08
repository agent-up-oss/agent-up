using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceListViewModel : ReactiveObject
{
    private readonly WorkspacesController _workspaces;
    private readonly Func<string, string, string?, Task>? _toggleControlMode;
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
            this.RaisePropertyChanged(nameof(ServerStatusText));
            this.RaisePropertyChanged(nameof(ServerStatusColor));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
            this.RaisePropertyChanged(nameof(ServerStatusText));
            this.RaisePropertyChanged(nameof(ServerStatusColor));
        }
    }

    public bool ShowEmptyState => _selectedWorkspace is null && _errorMessage is null && !_isLoading;
    public string ServerStatusText => _errorMessage is not null
        ? "SERVER OFFLINE"
        : _isLoading ? "CONNECTING" : "SERVER ONLINE";
    public string ServerStatusColor => _errorMessage is null ? "#00d66b" : "#d84f4f";

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

    public WorkspaceListViewModel(WorkspacesController workspaces, Func<string, string, string?, Task>? toggleControlMode = null)
    {
        _workspaces = workspaces;
        _toggleControlMode = toggleControlMode;
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsCollapsed = !IsCollapsed; });
    }

    public void ApplyControlMode(string workspaceId, string authority, int width, int height)
    {
        var item = Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        item?.ApplyControlMode(authority, width, height);
    }

    // Applies a state-change event from the server in-place, updating only the mutable state
    // fields on existing workspace and application view models. Does not clear or rebuild the
    // Workspaces collection, so SelectedWorkspace stays the same reference and no navigation
    // or browser-session reset is triggered.
    internal void ApplyEvent(
        string workspaceId,
        string newState,
        IReadOnlyList<AppStateChangeDto> appChanges,
        string? healthState)
    {
        var item = Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        item?.ApplyStateChange(newState, appChanges, healthState);
    }

    public async Task RefreshWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        try
        {
            var dto = await _workspaces.GetByIdAsync(workspaceId, ct);
            var existing = Workspaces.FirstOrDefault(w => w.Id == workspaceId);

            if (dto is null)
            {
                if (existing is not null)
                {
                    Workspaces.Remove(existing);
                    if (SelectedWorkspace?.Id == workspaceId)
                        SelectedWorkspace = Workspaces.FirstOrDefault();
                }
                ErrorMessage = null;
                return;
            }

            if (existing is not null)
            {
                existing.UpdateFrom(dto.State, dto.Applications);
                ErrorMessage = null;
                return;
            }

            var added = new WorkspaceItemViewModel(
                dto.Id, dto.DisplayName, dto.Branch, dto.RepositoryPath, dto.WorktreePath,
                dto.State, dto.Applications,
                toggleControlMode: _toggleControlMode is null ? null : (authority, preset) => _toggleControlMode(dto.Id, authority, preset));
            Workspaces.Add(added);
            if (SelectedWorkspace is null)
                SelectedWorkspace = added;
            ErrorMessage = null;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Could not refresh workspace '{workspaceId}': {ex.Message}";
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var dtos = await _workspaces.ListAsync(ct);

            // Merge in-place: update existing items, add new ones, remove deleted ones.
            // This keeps the same WorkspaceItemViewModel reference for existing workspaces so
            // SelectedWorkspace never changes reference — which would trigger the navigation
            // chain and reset the browser session URL.
            var existingById = Workspaces.ToDictionary(w => w.Id);
            var incomingById = dtos.ToDictionary(d => d.Id);

            foreach (var id in existingById.Keys.Except(incomingById.Keys).ToList())
            {
                Workspaces.Remove(existingById[id]);
                if (SelectedWorkspace?.Id == id)
                    SelectedWorkspace = null;
            }

            foreach (var dto in dtos)
            {
                if (existingById.TryGetValue(dto.Id, out var existing))
                {
                    existing.UpdateFrom(dto.State, dto.Applications);
                }
                else
                {
                    Workspaces.Add(new WorkspaceItemViewModel(
                        dto.Id, dto.DisplayName, dto.Branch, dto.RepositoryPath, dto.WorktreePath,
                        dto.State, dto.Applications,
                        toggleControlMode: _toggleControlMode is null ? null : (authority, preset) => _toggleControlMode(dto.Id, authority, preset)));
                }
            }

            if (SelectedWorkspace is null || !Workspaces.Any(w => w.Id == SelectedWorkspace.Id))
                SelectedWorkspace = Workspaces.FirstOrDefault();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Could not connect to Agent-Up Server: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
