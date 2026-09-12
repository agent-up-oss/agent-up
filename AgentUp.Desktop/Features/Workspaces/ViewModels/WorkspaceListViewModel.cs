using AgentUp.Desktop.Shared.Models;
using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceListViewModel : ReactiveObject, IWorkspaceItemHost
{
    private readonly WorkspacesController _workspaces;
    private WorkspaceItemViewModel? _selectedWorkspace;
    private bool _isCollapsed;
    private bool _isLoading;
    private string? _errorMessage;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspaceDeleteConfirmationViewModel DeleteConfirmation { get; }

    public WorkspaceCloneViewModel AddWorkspace { get; }

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
    public string ServerStatusColor => _errorMessage is null ? AgentUpThemeColors.AccentBright : AgentUpThemeColors.StatusDanger;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAddWorkspaceCommand { get; }

    public WorkspaceListViewModel(WorkspacesController workspaces)
    {
        _workspaces = workspaces;
        WorkspaceDeleteConfirmationViewModel? deleteConfirmation = null;
        deleteConfirmation = new WorkspaceDeleteConfirmationViewModel(
            id => DeleteWorkspaceAsync(id),
            () => deleteConfirmation!.Hide());
        DeleteConfirmation = deleteConfirmation;
        AddWorkspace = new WorkspaceCloneViewModel(
            (repository, branch) => CloneWorkspaceAsync(repository, branch));
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsCollapsed = !IsCollapsed; });
        ShowAddWorkspaceCommand = ReactiveCommand.Create(AddWorkspace.Show);
    }

    // Clones a repository into the Server-owned source clones root and selects the workspace the
    // Server registers for it. Returns false so the dialog stays open with the failure message.
    public async Task<bool> CloneWorkspaceAsync(string repository, string branch, CancellationToken ct = default)
    {
        try
        {
            var workspace = await _workspaces.CloneAsync(repository, branch, ct);
            ErrorMessage = null;
            await LoadAsync(ct);
            SelectWorkspace(workspace.Id);
            return true;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            AddWorkspace.ErrorMessage = ex.Message;
            return false;
        }
    }

    private void SelectWorkspace(string workspaceId)
    {
        var added = Workspaces.FirstOrDefault(workspace => workspace.Id == workspaceId);
        if (added is not null)
            SelectedWorkspace = added;
    }

    Task IWorkspaceItemHost.StartWorkspaceAsync(string workspaceId)
        => StartWorkspaceAsync(workspaceId);

    Task IWorkspaceItemHost.StopWorkspaceAsync(string workspaceId)
        => StopWorkspaceAsync(workspaceId);

    void IWorkspaceItemHost.RequestDeleteWorkspace(string workspaceId, string displayName)
        => DeleteConfirmation.Show(workspaceId, displayName);

    public async Task StartWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        try
        {
            await _workspaces.StartAsync(workspaceId, ct);
            ErrorMessage = null;
            await RefreshWorkspaceAsync(workspaceId, ct);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not start workspace: {ex.Message}";
        }
    }

    public async Task StopWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        try
        {
            await _workspaces.StopAsync(workspaceId, ct);
            ErrorMessage = null;
            await RefreshWorkspaceAsync(workspaceId, ct);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not stop workspace: {ex.Message}";
        }
    }

    public async Task DeleteWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        try
        {
            await _workspaces.DeleteAsync(workspaceId, ct);
            DeleteConfirmation.Hide();
            ErrorMessage = null;
            await RefreshWorkspaceAsync(workspaceId, ct);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not remove workspace: {ex.Message}";
        }
    }

    private WorkspaceItemViewModel CreateWorkspaceItem(WorkspaceDto dto)
    {
        var item = new WorkspaceItemViewModel(
            dto.Id,
            dto.DisplayName,
            dto.Branch,
            dto.RepositoryPath,
            dto.WorktreePath,
            dto.State,
            dto.Applications);
        item.SetHost(this);
        return item;
    }

    // Applies a state-change event from the server in-place, updating only the mutable state
    // fields on existing workspace and application view models. Does not clear or rebuild the
    // Workspaces collection, so SelectedWorkspace stays the same reference and no navigation
    // or browser-session reset is triggered.
    internal bool ApplyEvent(
        string workspaceId,
        string newState,
        IReadOnlyList<AppStateChangeDto> appChanges,
        string? healthState)
    {
        var item = Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (item is null)
            return false;

        item.ApplyStateChange(newState, appChanges, healthState);
        if (!string.Equals(newState, "Removed", StringComparison.Ordinal))
            ResortWorkspaces(workspaceId);
        return true;
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
                ResortWorkspaces(workspaceId);
                ErrorMessage = null;
                return;
            }

            var added = CreateWorkspaceItem(dto);
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
                    Workspaces.Add(CreateWorkspaceItem(dto));
                }
            }

            ApplyWorkspaceOrder(dtos);

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

    private void ApplyWorkspaceOrder(IReadOnlyList<WorkspaceDto> orderedDtos)
    {
        var itemsById = Workspaces.ToDictionary(w => w.Id);
        var orderedItems = orderedDtos
            .OrderByDescending(dto => WorkspaceListOrdering.ActivePriority(dto.State))
            .ThenByDescending(dto => dto.LastActivityAtUtc)
            .ThenBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(dto => itemsById.GetValueOrDefault(dto.Id))
            .Where(item => item is not null)
            .Cast<WorkspaceItemViewModel>()
            .ToList();

        if (orderedItems.Count != Workspaces.Count)
            return;

        Workspaces.Clear();
        foreach (var item in orderedItems)
            Workspaces.Add(item);
    }

    private void ResortWorkspaces(string? preferWithinTierId = null)
    {
        if (Workspaces.Count <= 1)
            return;

        var items = Workspaces.ToList();
        var active = items.Where(w => WorkspaceListOrdering.IsActive(w.State)).ToList();
        var inactive = items.Where(w => !WorkspaceListOrdering.IsActive(w.State)).ToList();

        PromoteWithinTier(active, preferWithinTierId);
        PromoteWithinTier(inactive, preferWithinTierId);

        var ordered = active.Concat(inactive).ToList();
        if (ordered.Select(w => w.Id).SequenceEqual(items.Select(w => w.Id)))
            return;

        Workspaces.Clear();
        foreach (var item in ordered)
            Workspaces.Add(item);
    }

    private static void PromoteWithinTier(List<WorkspaceItemViewModel> tier, string? workspaceId)
    {
        if (workspaceId is null)
            return;

        var index = tier.FindIndex(w => w.Id == workspaceId);
        if (index <= 0)
            return;

        var item = tier[index];
        tier.RemoveAt(index);
        tier.Insert(0, item);
    }
}
