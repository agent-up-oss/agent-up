using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Text.Json;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Audit.Services;

// Subscribes to every ReactiveUI property across all desktop viewmodels and reports
// changes to the server audit trail via POST /api/audit/record.
// Significant property changes are sent immediately; a full snapshot of all viewmodel
// and view state is also sent every 30 seconds.
// All HTTP calls are fire-and-forget so the desktop UI is never blocked.
// Agents query state via: audit_query(kind="desktop", source="viewmodel")
// For current state use: audit_query(kind="desktop", source="viewmodel", action="snapshot", limit=1)
public class ViewModelAuditor : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly IDisposable _snapshotTimer;
    private CompositeDisposable _vmSubs = new();
    private readonly SerialDisposable _selectedWsSubs = new();
    private readonly SerialDisposable _portTabSubs = new();
    private MainViewModel? _vm;
    private Func<IReadOnlyDictionary<string, string>>? _viewStateCapture;
    private bool _disposed;

    public ViewModelAuditor(HttpClient http)
    {
        _http = http;
        _snapshotTimer = Observable
            .Interval(TimeSpan.FromSeconds(30), RxApp.TaskpoolScheduler)
            .Subscribe(tick => { _ = SendSnapshotAsync(); });
    }

    public void Attach(MainViewModel vm, Func<IReadOnlyDictionary<string, string>> viewStateCapture)
    {
        _vmSubs.Dispose();
        _vmSubs = new CompositeDisposable();
        _selectedWsSubs.Disposable = null;
        _portTabSubs.Disposable = null;
        _vm = vm;
        _viewStateCapture = viewStateCapture;

        SubscribeMainViewModel(vm);
        SubscribeSidebar(vm.Sidebar);
        SubscribeApplications(vm.Applications);
        SubscribeConsole(vm.Console);
        SubscribeTutorial(vm.Tutorial);
        SubscribeSubTabs(vm);

        _ = SendSnapshotAsync();
    }

    // ─── MainViewModel ───────────────────────────────────────────────────────

    private void SubscribeMainViewModel(MainViewModel vm)
    {
        vm.WhenAnyValue(x => x.SelectedSubTab)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(tab =>
            {
                var wsId = _vm?.Sidebar.SelectedWorkspace?.Id;
                _ = RecordFieldAsync(wsId, "main.selectedSubTabType", tab?.GetType().Name ?? "null");
                _ = RecordFieldAsync(wsId, "main.selectedSubTabLabel", tab?.Label ?? string.Empty);
            })
            .DisposeWith(_vmSubs);

        vm.WhenAnyValue(x => x.AddressBarUrl)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .Subscribe(url => _ = RecordFieldAsync(_vm?.Sidebar.SelectedWorkspace?.Id, "main.addressBarUrl", url ?? string.Empty))
            .DisposeWith(_vmSubs);

        vm.WhenAnyValue(x => x.ShowConsole)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "main.showConsole", v.ToString()))
            .DisposeWith(_vmSubs);

        vm.WhenAnyValue(x => x.ShowPortView)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "main.showPortView", v.ToString()))
            .DisposeWith(_vmSubs);

        vm.WhenAnyValue(x => x.ShowTcpInfo)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "main.showTcpInfo", v.ToString()))
            .DisposeWith(_vmSubs);
    }

    // ─── WorkspaceListViewModel (Sidebar) ────────────────────────────────────

    private void SubscribeSidebar(WorkspaceListViewModel sidebar)
    {
        sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(ws =>
            {
                _ = RecordFieldAsync(ws?.Id, "sidebar.selectedWorkspaceId", ws?.Id ?? string.Empty);
                _ = RecordFieldAsync(ws?.Id, "sidebar.selectedWorkspaceDisplayName", ws?.DisplayName ?? string.Empty);
                _ = RecordFieldAsync(ws?.Id, "sidebar.selectedWorkspaceBranch", ws?.Branch ?? string.Empty);
                _ = RecordFieldAsync(ws?.Id, "sidebar.selectedWorkspaceRepositoryPath", ws?.RepositoryPath ?? string.Empty);
                _ = RecordFieldAsync(ws?.Id, "sidebar.selectedWorkspaceWorktreePath", ws?.WorktreePath ?? string.Empty);
                if (ws is not null)
                    SubscribeSelectedWorkspace(ws);
                else
                    _selectedWsSubs.Disposable = null;
            })
            .DisposeWith(_vmSubs);

        sidebar.WhenAnyValue(x => x.IsCollapsed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "sidebar.isCollapsed", v.ToString()))
            .DisposeWith(_vmSubs);

        sidebar.WhenAnyValue(x => x.IsLoading)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "sidebar.isLoading", v.ToString()))
            .DisposeWith(_vmSubs);

        sidebar.WhenAnyValue(x => x.ErrorMessage)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "sidebar.errorMessage", v ?? string.Empty))
            .DisposeWith(_vmSubs);

        sidebar.WhenAnyValue(x => x.ServerStatusText)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "sidebar.serverStatusText", v))
            .DisposeWith(_vmSubs);

        sidebar.WhenAnyValue(x => x.ServerStatusColor)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "sidebar.serverStatusColor", v))
            .DisposeWith(_vmSubs);
    }

    // ─── WorkspaceItemViewModel (selected workspace) ──────────────────────────

    private void SubscribeSelectedWorkspace(WorkspaceItemViewModel ws)
    {
        var inner = new CompositeDisposable();

        ws.WhenAnyValue(x => x.State)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(ws.Id, "workspace.state", v))
            .DisposeWith(inner);

        ws.WhenAnyValue(x => x.StateColor)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(ws.Id, "workspace.stateColor", v))
            .DisposeWith(inner);

        ws.WhenAnyValue(x => x.ControlAuthority)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(ws.Id, "workspace.controlAuthority", v))
            .DisposeWith(inner);

        // ControlLabel encodes both authority and viewport dimensions (e.g. "AI · 1280×720").
        ws.WhenAnyValue(x => x.ControlLabel)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(ws.Id, "workspace.controlLabel", v))
            .DisposeWith(inner);

        void OnAppsChanged(object? _, EventArgs __)
        {
            _ = RecordFieldAsync(ws.Id, "workspace.applicationCount", ws.Applications.Count.ToString());
            for (var i = 0; i < ws.Applications.Count; i++)
            {
                var app = ws.Applications[i];
                _ = RecordFieldAsync(ws.Id, $"workspace.app[{i}].name", app.Name);
                _ = RecordFieldAsync(ws.Id, $"workspace.app[{i}].state", app.State);
                _ = RecordFieldAsync(ws.Id, $"workspace.app[{i}].portCount", app.AllocatedPorts.Count.ToString());
            }
        }

        ws.ApplicationsChanged += OnAppsChanged;
        inner.Add(Disposable.Create(() => ws.ApplicationsChanged -= OnAppsChanged));

        foreach (var app in ws.Applications)
            SubscribeWorkspaceApp(ws.Id, app, inner);

        _selectedWsSubs.Disposable = inner;
    }

    private void SubscribeWorkspaceApp(string workspaceId, WorkspaceApplicationViewModel app, CompositeDisposable into)
    {
        app.WhenAnyValue(x => x.State)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(workspaceId, $"workspace.app.{app.Name}.state", v))
            .DisposeWith(into);
    }

    // ─── ApplicationListViewModel ─────────────────────────────────────────────

    private void SubscribeApplications(ApplicationListViewModel apps)
    {
        apps.WhenAnyValue(x => x.SelectedApplication)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(app =>
            {
                var wsId = _vm?.Sidebar.SelectedWorkspace?.Id;
                _ = RecordFieldAsync(wsId, "applications.selectedApplicationName", app?.Name ?? string.Empty);
                _ = RecordFieldAsync(wsId, "applications.selectedApplicationState", app?.State ?? string.Empty);
                _ = RecordFieldAsync(wsId, "applications.selectedApplicationCommand", app?.Command ?? string.Empty);
            })
            .DisposeWith(_vmSubs);
    }

    // ─── ConsoleViewModel ─────────────────────────────────────────────────────

    private void SubscribeConsole(ConsoleViewModel console)
    {
        console.WhenAnyValue(x => x.IsLoading)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "console.isLoading", v.ToString()))
            .DisposeWith(_vmSubs);

        console.WhenAnyValue(x => x.WasTruncated)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "console.wasTruncated", v.ToString()))
            .DisposeWith(_vmSubs);

        console.WhenAnyValue(x => x.HasHiddenLines)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "console.hasHiddenLines", v.ToString()))
            .DisposeWith(_vmSubs);

        console.WhenAnyValue(x => x.ShowAllLines)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "console.showAllLines", v.ToString()))
            .DisposeWith(_vmSubs);
    }

    // ─── FirstRunTutorialViewModel ────────────────────────────────────────────

    private void SubscribeTutorial(FirstRunTutorialViewModel tutorial)
    {
        tutorial.WhenAnyValue(x => x.IsVisible)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.isVisible", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.CurrentStep)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.currentStep", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.StatusMessage)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.statusMessage", v ?? string.Empty))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.DockerCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.dockerCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.NodeCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.nodeCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.ProjectFilesCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.projectFilesCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.AgentUpJsonCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.agentUpJsonCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.WorkspaceCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.workspaceCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.DuplicateCheckPassed)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.duplicateCheckPassed", v.ToString()))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.ProjectDirectory)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.projectDirectory", v ?? string.Empty))
            .DisposeWith(_vmSubs);

        tutorial.WhenAnyValue(x => x.CanContinue)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(v => _ = RecordFieldAsync(null, "tutorial.canContinue", v.ToString()))
            .DisposeWith(_vmSubs);
    }

    // ─── SubTabs (PortSubTabViewModel + ConsoleSubTabViewModel) ──────────────

    private void SubscribeSubTabs(MainViewModel vm)
    {
        vm.WhenAnyValue(x => x.SelectedSubTab)
            .Skip(1)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(_ => RebindPortTabSubscriptions(vm))
            .DisposeWith(_vmSubs);

        void OnSubTabsChanged(object? _, System.Collections.Specialized.NotifyCollectionChangedEventArgs __)
            => RebindPortTabSubscriptions(vm);

        vm.SubTabs.CollectionChanged += OnSubTabsChanged;
        _vmSubs.Add(Disposable.Create(() => vm.SubTabs.CollectionChanged -= OnSubTabsChanged));
    }

    private void RebindPortTabSubscriptions(MainViewModel vm)
    {
        var inner = new CompositeDisposable();
        var wsId = vm.Sidebar.SelectedWorkspace?.Id;
        var portTabs = vm.SubTabs.OfType<PortSubTabViewModel>().ToList();

        _ = RecordFieldAsync(wsId, "subTabs.count", vm.SubTabs.Count.ToString());
        _ = RecordFieldAsync(wsId, "subTabs.portTabCount", portTabs.Count.ToString());

        for (var i = 0; i < portTabs.Count; i++)
        {
            var idx = i;
            var tab = portTabs[i];
            _ = RecordFieldAsync(wsId, $"subTabs.port[{idx}].defaultPort", tab.DefaultPort.ToString());
            _ = RecordFieldAsync(wsId, $"subTabs.port[{idx}].allocatedPort", tab.AllocatedPort.ToString());
            _ = RecordFieldAsync(wsId, $"subTabs.port[{idx}].protocol", tab.Protocol);
            _ = RecordFieldAsync(wsId, $"subTabs.port[{idx}].isHttp", tab.IsHttp.ToString());
            _ = RecordFieldAsync(wsId, $"subTabs.port[{idx}].url", tab.Url);

            // Probe timer fires every 3 s — only log when the open/closed state actually changes.
            tab.WhenAnyValue(x => x.IsOpen)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromSeconds(5), RxApp.TaskpoolScheduler)
                .Subscribe(open =>
                {
                    var currentWsId = _vm?.Sidebar.SelectedWorkspace?.Id;
                    _ = RecordFieldAsync(currentWsId, $"subTabs.port[{idx}].isOpen", open.ToString());
                })
                .DisposeWith(inner);
        }

        _portTabSubs.Disposable = inner;
    }

    // ─── Snapshot (full state every 30 s) ────────────────────────────────────

    private async Task SendSnapshotAsync()
    {
        if (_vm is null) return;
        try
        {
            var details = BuildSnapshot();
            if (_viewStateCapture is not null)
            {
                foreach (var (k, v) in _viewStateCapture())
                    details[k] = v;
            }

            var wsId = _vm.Sidebar.SelectedWorkspace?.Id;
            await PostAuditAsync("desktop", "viewmodel", "snapshot", "info", wsId, details);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            System.Diagnostics.Trace.TraceWarning($"[ViewModelAuditor] Snapshot failed: {ex.Message}");
        }
    }

    private Dictionary<string, string> BuildSnapshot()
    {
        var f = new Dictionary<string, string>();
        if (_vm is null) return f;

        var vm = _vm;
        var sidebar = vm.Sidebar;
        var ws = sidebar.SelectedWorkspace;

        // ── MainViewModel ──
        f["main.selectedSubTabType"] = vm.SelectedSubTab?.GetType().Name ?? "null";
        f["main.selectedSubTabLabel"] = vm.SelectedSubTab?.Label ?? string.Empty;
        f["main.addressBarUrl"] = vm.AddressBarUrl ?? string.Empty;
        f["main.showConsole"] = vm.ShowConsole.ToString();
        f["main.showPortView"] = vm.ShowPortView.ToString();
        f["main.showTcpInfo"] = vm.ShowTcpInfo.ToString();
        f["main.subTabCount"] = vm.SubTabs.Count.ToString();

        // ── WorkspaceListViewModel ──
        f["sidebar.selectedWorkspaceId"] = ws?.Id ?? string.Empty;
        f["sidebar.selectedWorkspaceDisplayName"] = ws?.DisplayName ?? string.Empty;
        f["sidebar.selectedWorkspaceBranch"] = ws?.Branch ?? string.Empty;
        f["sidebar.selectedWorkspaceRepositoryPath"] = ws?.RepositoryPath ?? string.Empty;
        f["sidebar.selectedWorkspaceWorktreePath"] = ws?.WorktreePath ?? string.Empty;
        f["sidebar.isCollapsed"] = sidebar.IsCollapsed.ToString();
        f["sidebar.isExpanded"] = sidebar.IsExpanded.ToString();
        f["sidebar.isLoading"] = sidebar.IsLoading.ToString();
        f["sidebar.errorMessage"] = sidebar.ErrorMessage ?? string.Empty;
        f["sidebar.serverStatusText"] = sidebar.ServerStatusText;
        f["sidebar.serverStatusColor"] = sidebar.ServerStatusColor;
        f["sidebar.workspaceCount"] = sidebar.Workspaces.Count.ToString();
        f["sidebar.showEmptyState"] = sidebar.ShowEmptyState.ToString();
        f["sidebar.allWorkspacesSummary"] = string.Join("; ", sidebar.Workspaces.Select(w =>
            $"{w.DisplayName}[{w.Branch}]={w.State}"));

        // ── WorkspaceItemViewModel (selected workspace) ──
        if (ws is not null)
        {
            f["workspace.id"] = ws.Id;
            f["workspace.displayName"] = ws.DisplayName;
            f["workspace.branch"] = ws.Branch;
            f["workspace.repositoryPath"] = ws.RepositoryPath;
            f["workspace.repositoryName"] = ws.RepositoryName;
            f["workspace.repositoryToolTip"] = ws.RepositoryToolTip;
            f["workspace.worktreePath"] = ws.WorktreePath;
            f["workspace.state"] = ws.State;
            f["workspace.stateColor"] = ws.StateColor;
            f["workspace.controlAuthority"] = ws.ControlAuthority;
            f["workspace.controlLabel"] = ws.ControlLabel;
            f["workspace.initials"] = ws.Initials;
            f["workspace.applicationCount"] = ws.Applications.Count.ToString();

            for (var i = 0; i < ws.Applications.Count; i++)
            {
                var app = ws.Applications[i];
                f[$"workspace.app[{i}].name"] = app.Name;
                f[$"workspace.app[{i}].state"] = app.State;
                f[$"workspace.app[{i}].stateColor"] = app.StateColor;
                f[$"workspace.app[{i}].command"] = app.Command;
                f[$"workspace.app[{i}].portCount"] = app.AllocatedPorts.Count.ToString();
                for (var p = 0; p < app.AllocatedPorts.Count; p++)
                {
                    var port = app.AllocatedPorts[p];
                    f[$"workspace.app[{i}].port[{p}].variable"] = port.Variable ?? string.Empty;
                    f[$"workspace.app[{i}].port[{p}].defaultPort"] = port.DefaultPort.ToString();
                    f[$"workspace.app[{i}].port[{p}].allocatedPort"] = port.AllocatedPort.ToString();
                    f[$"workspace.app[{i}].port[{p}].protocol"] = port.Protocol;
                }
            }
        }

        // ── ApplicationListViewModel ──
        var selApp = vm.Applications.SelectedApplication;
        f["applications.selectedApplicationName"] = selApp?.Name ?? string.Empty;
        f["applications.selectedApplicationState"] = selApp?.State ?? string.Empty;
        f["applications.selectedApplicationCommand"] = selApp?.Command ?? string.Empty;
        f["applications.applicationCount"] = vm.Applications.Applications.Count.ToString();
        f["applications.allApplicationNames"] = string.Join(", ", vm.Applications.Applications.Select(a => a.Name));

        // ── ConsoleViewModel ──
        f["console.isLoading"] = vm.Console.IsLoading.ToString();
        f["console.wasTruncated"] = vm.Console.WasTruncated.ToString();
        f["console.hasHiddenLines"] = vm.Console.HasHiddenLines.ToString();
        f["console.showAllLines"] = vm.Console.ShowAllLines.ToString();
        f["console.lineCount"] = vm.Console.Lines.Count.ToString();

        // ── SubTabs ──
        var portTabs = vm.SubTabs.OfType<PortSubTabViewModel>().ToList();
        f["subTabs.count"] = vm.SubTabs.Count.ToString();
        f["subTabs.portTabCount"] = portTabs.Count.ToString();
        f["subTabs.hasConsoleTab"] = vm.SubTabs.OfType<ConsoleSubTabViewModel>().Any().ToString();
        for (var i = 0; i < portTabs.Count; i++)
        {
            var tab = portTabs[i];
            f[$"subTabs.port[{i}].label"] = tab.Label;
            f[$"subTabs.port[{i}].defaultPort"] = tab.DefaultPort.ToString();
            f[$"subTabs.port[{i}].allocatedPort"] = tab.AllocatedPort.ToString();
            f[$"subTabs.port[{i}].isHttp"] = tab.IsHttp.ToString();
            f[$"subTabs.port[{i}].isOpen"] = tab.IsOpen.ToString();
            f[$"subTabs.port[{i}].statusColor"] = tab.StatusColor;
            f[$"subTabs.port[{i}].protocol"] = tab.Protocol;
            f[$"subTabs.port[{i}].url"] = tab.Url;
            f[$"subTabs.port[{i}].variable"] = tab.Variable ?? string.Empty;
        }

        // ── FirstRunTutorialViewModel ──
        var tut = vm.Tutorial;
        f["tutorial.isVisible"] = tut.IsVisible.ToString();
        f["tutorial.currentStep"] = tut.CurrentStep.ToString();
        f["tutorial.canContinue"] = tut.CanContinue.ToString();
        f["tutorial.canGoBack"] = tut.CanGoBack.ToString();
        f["tutorial.statusMessage"] = tut.StatusMessage ?? string.Empty;
        f["tutorial.projectDirectory"] = tut.ProjectDirectory ?? string.Empty;
        f["tutorial.dockerCheckPassed"] = tut.DockerCheckPassed.ToString();
        f["tutorial.environmentSelected"] = tut.EnvironmentSelected.ToString();
        f["tutorial.nodeCheckPassed"] = tut.NodeCheckPassed.ToString();
        f["tutorial.projectFilesCreated"] = tut.ProjectFilesCreated.ToString();
        f["tutorial.projectFilesCheckPassed"] = tut.ProjectFilesCheckPassed.ToString();
        f["tutorial.agentUpJsonActionTaken"] = tut.AgentUpJsonActionTaken.ToString();
        f["tutorial.agentUpJsonCheckPassed"] = tut.AgentUpJsonCheckPassed.ToString();
        f["tutorial.startCommandSucceeded"] = tut.StartCommandSucceeded.ToString();
        f["tutorial.workspaceCheckPassed"] = tut.WorkspaceCheckPassed.ToString();
        f["tutorial.duplicateActionTaken"] = tut.DuplicateActionTaken.ToString();
        f["tutorial.duplicateCheckPassed"] = tut.DuplicateCheckPassed.ToString();

        return f;
    }

    // ─── HTTP helpers ─────────────────────────────────────────────────────────

    private async Task RecordFieldAsync(string? workspaceId, string field, string value)
    {
        await PostAuditAsync("desktop", "viewmodel", "field_change", "info", workspaceId,
            new Dictionary<string, string> { ["field"] = field, ["value"] = value });
    }

    private async Task PostAuditAsync(
        string kind, string source, string action, string outcome,
        string? workspaceId, IReadOnlyDictionary<string, string> details)
    {
        try
        {
            var dto = new
            {
                kind,
                source,
                action,
                outcome,
                workspaceId,
                details
            };
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var _ = await _http.PostAsync("/api/audit/record", content);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ObjectDisposedException)
        {
            Trace.TraceWarning(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _snapshotTimer.Dispose();
        _selectedWsSubs.Dispose();
        _portTabSubs.Dispose();
        _vmSubs.Dispose();
    }
}
