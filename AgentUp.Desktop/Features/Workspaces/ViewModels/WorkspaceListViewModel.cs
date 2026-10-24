using AgentUp.Desktop.Shared.Models;
using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Entitlements.Controllers;
using AgentUp.Desktop.Features.Entitlements.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceListViewModel : ReactiveObject, IWorkspaceItemHost
{
    private readonly WorkspacesController _workspaces;
    private readonly EntitlementsController? _entitlements;
    private WorkspaceItemViewModel? _selectedWorkspace;
    private bool _isCollapsed;
    private bool _isLoading;
    private bool _canCreateWorkspace = true;
    private bool _canDeleteWorkspace = true;
    private bool _canWriteGit = true;
    private string? _errorMessage;

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = [];

    public WorkspaceDeleteConfirmationViewModel DeleteConfirmation { get; }

    public WorkspaceCloneViewModel AddWorkspace { get; }

    public WorkspaceRefreshViewModel RefreshReview { get; }

    public PlanCardViewModel Plan { get; } = new();

    public WorkspaceItemViewModel? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedWorkspace, value);
            this.RaisePropertyChanged(nameof(ShowEmptyState));
            this.RaisePropertyChanged(nameof(ShowSidebarPlan));
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
            this.RaisePropertyChanged(nameof(ShowSidebarPlan));
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
            this.RaisePropertyChanged(nameof(ShowSidebarPlan));
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
            this.RaisePropertyChanged(nameof(ShowSidebarPlan));
            this.RaisePropertyChanged(nameof(ServerStatusText));
            this.RaisePropertyChanged(nameof(ServerStatusColor));
        }
    }

    public bool RequiresSignIn { get; private set; }

    public bool CanCreateWorkspace
    {
        get => _canCreateWorkspace;
        private set
        {
            this.RaiseAndSetIfChanged(ref _canCreateWorkspace, value);
            this.RaisePropertyChanged(nameof(EmptyStateHint));
        }
    }

    public bool CanDeleteWorkspace
    {
        get => _canDeleteWorkspace;
        private set
        {
            this.RaiseAndSetIfChanged(ref _canDeleteWorkspace, value);
            ApplyDeleteAvailability();
        }
    }

    public bool CanWriteGit
    {
        get => _canWriteGit;
        private set => this.RaiseAndSetIfChanged(ref _canWriteGit, value);
    }

    public string EmptyStateHint => CanCreateWorkspace
        ? "Clone a repository with + or run agent-up start in a project that has agent-up.json."
        : "This Server does not allow adding workspaces from the client.";

    public bool ShowEmptyState => _selectedWorkspace is null && _errorMessage is null && !_isLoading;

    public bool ShowSidebarPlan => Plan.IsVisible && IsExpanded && !ShowEmptyState;
    public string ServerStatusText => _errorMessage is not null
        ? "SERVER OFFLINE"
        : _isLoading ? "CONNECTING" : "SERVER ONLINE";
    public string ServerStatusColor => _errorMessage is null ? AgentUpThemeColors.AccentBright : AgentUpThemeColors.StatusDanger;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAddWorkspaceCommand { get; }

    public WorkspaceListViewModel(WorkspacesController workspaces, EntitlementsController? entitlements = null)
    {
        _workspaces = workspaces;
        _entitlements = entitlements;
        WorkspaceDeleteConfirmationViewModel? deleteConfirmation = null;
        deleteConfirmation = new WorkspaceDeleteConfirmationViewModel(
            id => DeleteWorkspaceAsync(id),
            () => deleteConfirmation!.Hide());
        DeleteConfirmation = deleteConfirmation;
        AddWorkspace = new WorkspaceCloneViewModel(
            (repository, revision, derived) => CloneWorkspaceAsync(repository, revision, derived));
        RefreshReview = new WorkspaceRefreshViewModel(
            (workspaceId, revision) => RefreshDerivedWorkspaceAsync(workspaceId, revision));
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        ToggleCommand = ReactiveCommand.Create(() => { IsCollapsed = !IsCollapsed; });
        ShowAddWorkspaceCommand = ReactiveCommand.Create(
            AddWorkspace.Show,
            this.WhenAnyValue(x => x.CanCreateWorkspace));
    }

    // Clones a repository into the Server-owned source clones root and selects the workspace the
    // Server registers for it. Returns false so the dialog stays open with the failure message.
    public async Task<bool> CloneWorkspaceAsync(
        string repository,
        string revision,
        bool derived = false,
        CancellationToken ct = default)
    {
        try
        {
            var workspace = await _workspaces.CloneAsync(repository, revision, derived, ct);
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

    void IWorkspaceItemHost.ShowRefreshDerivedWorkspace(string workspaceId, string currentRevision)
        => RefreshReview.Show(workspaceId, currentRevision);

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

    public async Task<bool> RefreshDerivedWorkspaceAsync(string workspaceId, string? revision = null, CancellationToken ct = default)
    {
        try
        {
            var workspace = await _workspaces.RefreshCloneAsync(workspaceId, revision, ct);
            ErrorMessage = null;
            await RefreshWorkspaceAsync(workspace.Id, ct);
            return true;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            ErrorMessage = $"Could not refresh review workspace: {ex.Message}";
            RefreshReview.ErrorMessage = ex.Message;
            return false;
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
            dto.Applications,
            dto.Derived,
            dto.SourceRevision);
        item.SetHost(this);
        item.CanDelete = CanDeleteWorkspace;
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
        RequiresSignIn = false;
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

            await LoadPlanAsync(ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Sign in required.";
            RequiresSignIn = true;
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

    public void Disconnect()
    {
        Workspaces.Clear();
        SelectedWorkspace = null;
        ErrorMessage = null;
        RequiresSignIn = false;
        IsLoading = false;
        CanCreateWorkspace = true;
        CanDeleteWorkspace = true;
        CanWriteGit = true;
        Plan.Clear();
    }

    private async Task LoadPlanAsync(CancellationToken ct)
    {
        if (_entitlements is null)
        {
            CanCreateWorkspace = true;
            CanDeleteWorkspace = true;
            CanWriteGit = true;
            Plan.Clear();
            this.RaisePropertyChanged(nameof(ShowSidebarPlan));
            return;
        }

        var plan = await _entitlements.GetPlanAsync(ct);
        CanCreateWorkspace = plan.CanCreateWorkspace;
        CanDeleteWorkspace = plan.CanDeleteWorkspace;
        CanWriteGit = plan.CanWriteGit;
        Plan.Apply(plan.Card);
        this.RaisePropertyChanged(nameof(ShowSidebarPlan));
    }

    private void ApplyDeleteAvailability()
    {
        foreach (var workspace in Workspaces)
            workspace.CanDelete = CanDeleteWorkspace;
    }

    private void ApplyWorkspaceOrder(IReadOnlyList<WorkspaceDto> orderedDtos)
    {
        var itemsById = Workspaces.ToDictionary(w => w.Id);
        var orderedItems = orderedDtos
            .OrderByDescending(dto => WorkspaceListOrdering.OwnWorkspacePriority(dto.Derived))
            .ThenByDescending(dto => WorkspaceListOrdering.ActivePriority(dto.State))
            .ThenByDescending(dto => dto.LastActivityAtUtc)
            .ThenBy(dto => dto.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(dto => itemsById.GetValueOrDefault(dto.Id))
            .Where(item => item is not null)
            .Cast<WorkspaceItemViewModel>()
            .ToList();

        if (orderedItems.Count != Workspaces.Count)
            return;

        Reorder(orderedItems);
    }

    private void ResortWorkspaces(string? preferWithinTierId = null)
    {
        if (Workspaces.Count <= 1)
            return;

        var items = Workspaces.ToList();
        var ownActive = items.Where(w => !w.Derived && WorkspaceListOrdering.IsActive(w.State)).ToList();
        var derivedActive = items.Where(w => w.Derived && WorkspaceListOrdering.IsActive(w.State)).ToList();
        var ownInactive = items.Where(w => !w.Derived && !WorkspaceListOrdering.IsActive(w.State)).ToList();
        var derivedInactive = items.Where(w => w.Derived && !WorkspaceListOrdering.IsActive(w.State)).ToList();

        PromoteWithinTier(ownActive, preferWithinTierId);
        PromoteWithinTier(derivedActive, preferWithinTierId);
        PromoteWithinTier(ownInactive, preferWithinTierId);
        PromoteWithinTier(derivedInactive, preferWithinTierId);

        var ordered = ownActive.Concat(derivedActive).Concat(ownInactive).Concat(derivedInactive).ToList();
        if (ordered.Select(w => w.Id).SequenceEqual(items.Select(w => w.Id)))
            return;

        Reorder(ordered);
    }

    // Clearing the bound collection makes the list control drop its selection and write the null
    // back here, so a reorder would silently deselect the open workspace. Put the same item back
    // afterwards: it is the same reference, so no navigation is triggered.
    private void Reorder(IReadOnlyList<WorkspaceItemViewModel> ordered)
    {
        var selected = SelectedWorkspace;

        Workspaces.Clear();
        foreach (var item in ordered)
            Workspaces.Add(item);

        SelectedWorkspace = selected is not null && Workspaces.Contains(selected)
            ? selected
            : Workspaces.FirstOrDefault();
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
