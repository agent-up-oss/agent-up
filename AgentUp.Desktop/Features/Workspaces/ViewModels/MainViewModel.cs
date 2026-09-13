using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Audit.ViewModels;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.Database.ViewModels;
using AgentUp.Desktop.Features.Git.ViewModels;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Metrics.ViewModels;
using AgentUp.Desktop.Features.Ports.Controllers;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Validation.Controllers;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels.Chrome;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject, IValidationReplayHost
{
    private SubTabViewModel? _selectedSubTab;
    private string? _addressBarUrl;
    private readonly PortsController _ports;
    private readonly Subject<(string? WorkspaceId, string? Url)> _addressNavigations = new();
    private readonly Subject<BrowserCommand> _browserCommands = new();
    // Last-visited URL per port origin (e.g. "http://localhost:10100" → "http://localhost:10100/docs/intro").
    // Used to restore the exact page when the user switches away and back to an HTTP tab.
    private readonly Dictionary<string, string> _portUrls = new();
    private WorkspaceItemViewModel? _workspaceApplicationSubscription;
    private string? _lastSelectedHttpPortKey;
    private CancellationTokenSource? _metricsLoadCts;
    private WorkspaceShellTab _selectedShellTab = WorkspaceShellTab.Overview;
    private WorkspaceShellTabItemViewModel? _selectedShellTabItem;
    private readonly ValidationController? _validationController;
    private CancellationTokenSource? _validationLoad;
    private Func<string, string, Task<string?>>? _validationEvalAsync;

    public WorkspaceListViewModel Sidebar { get; }
    public ApplicationListViewModel Applications { get; }
    public ConsoleViewModel Console { get; }
    public MetricsViewModel Metrics { get; }
    public DatabaseViewModel Database { get; }
    public ApplicationAuditViewModel Audit { get; }
    public GitPanelViewModel Git { get; }
    public AgentChatViewModel Agent { get; }
    public WorkspaceOverviewViewModel Overview { get; }
    public FirstRunTutorialViewModel Tutorial { get; }
    public LoginViewModel Login { get; }
    public WindowChromeViewModel Chrome { get; } = new();
    public ValidationViewModel? Validation { get; }
    internal IValidationReplayConnector? ValidationReplay { get; }
    public bool IsValidationOpen => Validation is { IsCollapsed: false };

    public ObservableCollection<WorkspaceShellTabItemViewModel> ShellTabs { get; } =
    [
        new(WorkspaceShellTab.Overview, "Overview"),
        new(WorkspaceShellTab.Agent, "Agent"),
        new(WorkspaceShellTab.Commit, "Commit")
    ];

    public WorkspaceShellTab SelectedShellTab
    {
        get => _selectedShellTab;
        set
        {
            if (_selectedShellTab == value)
            {
                SyncShellTabItem();
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedShellTab, value);
            Git.IsVisible = value == WorkspaceShellTab.Commit;
            Agent.IsVisible = value == WorkspaceShellTab.Agent;
            SyncShellTabItem();
            RaiseShellVisibility();
            if (value == WorkspaceShellTab.Overview && Sidebar.SelectedWorkspace?.Id is { } overviewId)
                _ = Overview.LoadAsync(overviewId);
        }
    }

    public WorkspaceShellTabItemViewModel? SelectedShellTabItem
    {
        get => _selectedShellTabItem;
        set
        {
            if (value is null)
                return;

            SelectedShellTab = value.Kind;
        }
    }

    public ApplicationViewModel? SelectedApplicationTab
    {
        get => ShowApplication ? Applications.SelectedApplication : null;
        set
        {
            if (value is null)
                return;

            Applications.SelectedApplication = value;
            SelectedShellTab = WorkspaceShellTab.Application;
        }
    }

    private readonly ChromeServerStatusViewModel _chromeServerStatus;

    public ObservableCollection<SubTabViewModel> SubTabs { get; } = [];

    public SubTabViewModel? SelectedSubTab
    {
        get => _selectedSubTab;
        set => this.RaiseAndSetIfChanged(ref _selectedSubTab, value);
    }

    public bool ShowOverview => SelectedShellTab == WorkspaceShellTab.Overview;
    public bool ShowAgent => SelectedShellTab == WorkspaceShellTab.Agent;
    public bool ShowCommit => SelectedShellTab == WorkspaceShellTab.Commit;
    public bool ShowApplication => SelectedShellTab == WorkspaceShellTab.Application;
    public bool ShowApplicationChrome => ShowApplication && Applications.SelectedApplication is not null;
    public bool ShowNoApplications => ShowApplication && Applications.SelectedApplication is null;
    public bool ShowConsole => ShowApplication && SelectedSubTab is ConsoleSubTabViewModel;
    public bool ShowMetrics => ShowApplication && SelectedSubTab is MetricsSubTabViewModel;
    public bool ShowDatabase => ShowApplication && SelectedSubTab is DatabaseSubTabViewModel;
    public bool ShowAudit => ShowApplication && SelectedSubTab is AuditSubTabViewModel;
    public bool ShowPortView => ShowApplication && SelectedSubTab is PortSubTabViewModel { IsHttp: true };
    public bool ShowDesktopView => ShowApplication && SelectedSubTab is DesktopSubTabViewModel;
    public bool ShowDisplayView => ShowPortView || ShowDesktopView;
    public bool ShowTcpInfo => ShowApplication && SelectedSubTab is PortSubTabViewModel { IsHttp: false };

    public string? AddressBarUrl
    {
        get => _addressBarUrl;
        set => this.RaiseAndSetIfChanged(ref _addressBarUrl, value);
    }

    public ReactiveCommand<Unit, Unit> NavigateAddressCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowserBackCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowserForwardCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowserReloadCommand { get; }

    // Emits (workspaceId, url) when the browser should navigate.
    // workspaceId drives which isolated session to use; url is the destination.
    public IObservable<(string? WorkspaceId, string? Url)> BrowserNavigation { get; }
    public IObservable<(string? WorkspaceId, string? Url)> BrowserTabNavigation { get; }
    public IObservable<BrowserCommand> BrowserCommands => _browserCommands;

    public MainViewModel(
        WorkspaceListViewModel sidebar,
        ApplicationListViewModel applications,
        ConsoleViewModel console,
        MetricsViewModel metrics,
        DatabaseViewModel database,
        ApplicationAuditViewModel audit,
        GitPanelViewModel git,
        AgentChatViewModel agent,
        WorkspaceOverviewViewModel overview,
        FirstRunTutorialViewModel tutorial,
        LoginViewModel login,
        PortsController ports,
        ValidationViewModel? validation = null,
        IValidationReplayConnector? validationReplay = null)
    {
        Sidebar = sidebar;
        Applications = applications;
        Console = console;
        Metrics = metrics;
        Database = database;
        Audit = audit;
        Git = git;
        Agent = agent;
        Overview = overview;
        Tutorial = tutorial;
        Login = login;
        _ports = ports;
        Validation = validation;
        ValidationReplay = validationReplay;
        _validationController = validation is null ? null : new ValidationController(validation);
        if (validation is not null)
            validation.WhenAnyValue(x => x.IsCollapsed)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(IsValidationOpen)));
        _chromeServerStatus = new ChromeServerStatusViewModel(sidebar);
        UpdateChromeLeftItems(Login.IsVisible);
        Login.WhenAnyValue(viewModel => viewModel.IsVisible)
            .Subscribe(UpdateChromeLeftItems);

        NavigateAddressCommand = ReactiveCommand.Create(NavigateAddress);
        BrowserBackCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Back));
        BrowserForwardCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Forward));
        BrowserReloadCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Reload));

        SyncShellTabItem();

        var selectedPortTab = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab as PortSubTabViewModel);

        SubscribeWorkspaceSelection();
        SubscribeApplicationSelection();
        SubscribeSubTabSelection();
        SubscribeMetricsRefresh();
        SubscribeOverviewRefresh();
        SubscribeTutorialSteps();
        SubscribeSelectedPortProbe(selectedPortTab);

        BrowserTabNavigation = CreateBrowserTabNavigation();
        BrowserNavigation = CreateBrowserNavigation(selectedPortTab);
    }

    private void SubscribeWorkspaceSelection()
        => Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Subscribe(ws =>
            {
                Console.Clear();
                Database.Clear();
                CancelPendingMetricsLoad();
                Metrics.Clear();
                SubscribeSelectedWorkspaceApplications(ws);
                UpdateApplicationsFromWorkspace(ws, preserveSelection: false);
                SelectedShellTab = WorkspaceShellTab.Overview;
                if (ws is null)
                    Overview.Clear();
                else
                    _ = Overview.LoadAsync(ws.Id);
                Git.PrepareWorkspace(ws?.Id, ws?.Branch);
                _ = Git.LoadAsync(ws?.Id);
                _ = Agent.LoadAsync(ws?.Id);
                LoadValidation();
            });

    private void CancelPendingMetricsLoad()
    {
        _metricsLoadCts?.Cancel();
        _metricsLoadCts?.Dispose();
        _metricsLoadCts = null;
    }

    private void SubscribeSelectedWorkspaceApplications(WorkspaceItemViewModel? workspace)
    {
        if (_workspaceApplicationSubscription is not null)
            _workspaceApplicationSubscription.ApplicationsChanged -= HandleSelectedWorkspaceApplicationsChanged;

        _workspaceApplicationSubscription = workspace;

        if (_workspaceApplicationSubscription is not null)
            _workspaceApplicationSubscription.ApplicationsChanged += HandleSelectedWorkspaceApplicationsChanged;
    }

    private void HandleSelectedWorkspaceApplicationsChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, Sidebar.SelectedWorkspace)) return;
        UpdateApplicationsFromWorkspace(Sidebar.SelectedWorkspace, preserveSelection: true);

        var selectedAppName = Applications.SelectedApplication?.Name;
        if (selectedAppName is not null)
        {
            var wsApp = Sidebar.SelectedWorkspace?.Applications
                .FirstOrDefault(a => string.Equals(a.Name, selectedAppName, StringComparison.Ordinal));
            if (wsApp is not null)
                ApplyPortHealthToSubTabs(wsApp);
        }
        this.RaisePropertyChanged(nameof(ShowDesktopView));
        this.RaisePropertyChanged(nameof(ShowDisplayView));

        // Navigate even when the console or TCP tab is active so the direct browser reconnects
        // when the workspace starts remotely while the user is viewing a non-port tab.
        var pt = SelectedSubTab as PortSubTabViewModel
            ?? SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault(t => t.IsHttp);
        if (pt is { IsHttp: true })
            _addressNavigations.OnNext((Sidebar.SelectedWorkspace?.Id, GetPortNavigationUrl(pt)));
    }

    private void UpdateApplicationsFromWorkspace(WorkspaceItemViewModel? workspace, bool preserveSelection)
    {
        var selectedName = preserveSelection ? Applications.SelectedApplication?.Name : null;
        Applications.Update(workspace?.Applications.Select(CreateApplicationViewModel).ToList() ?? []);

        if (selectedName is null) return;

        var selectedApplication = Applications.Applications.FirstOrDefault(app => app.Name == selectedName);
        if (selectedApplication is not null)
            Applications.SelectedApplication = selectedApplication;
    }

    private void SubscribeApplicationSelection()
        => Applications.WhenAnyValue(x => x.SelectedApplication)
            .Subscribe(app =>
            {
                RebuildSubTabs(app);
                this.RaisePropertyChanged(nameof(ShowApplicationChrome));
                this.RaisePropertyChanged(nameof(ShowNoApplications));
                this.RaisePropertyChanged(nameof(SelectedApplicationTab));
                LoadValidation();
                if (app is null) return;
                var workspaceId = Sidebar.SelectedWorkspace?.Id;
                if (workspaceId is not null)
                {
                    _ = Console.LoadAsync(workspaceId, app.Name);
                    if (SelectedSubTab is MetricsSubTabViewModel)
                        LoadMetricsIfPossible();
                }
            });

    private void SubscribeMetricsRefresh()
        => this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab is MetricsSubTabViewModel
                ? Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(30), RxApp.TaskpoolScheduler)
                : Observable.Empty<long>())
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => LoadMetricsIfPossible());

    private void SubscribeOverviewRefresh()
        => Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .CombineLatest(this.WhenAnyValue(x => x.SelectedShellTab), (workspace, tab) => (workspace, tab))
            .Select(state => state.tab == WorkspaceShellTab.Overview && state.workspace is not null
                ? Observable.Timer(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), RxApp.TaskpoolScheduler)
                    .Select(_ => state.workspace!.Id)
                : Observable.Empty<string>())
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(workspaceId => _ = Overview.LoadAsync(workspaceId));

    private void LoadMetricsIfPossible()
    {
        var workspaceId = Sidebar.SelectedWorkspace?.Id;
        var appName = Applications.SelectedApplication?.Name;

        CancelPendingMetricsLoad();

        if (workspaceId is null || appName is null)
            return;

        var cts = new CancellationTokenSource();
        _metricsLoadCts = cts;
        _ = Metrics.LoadAsync(workspaceId, appName, cts.Token);
    }


    private void SubscribeSubTabSelection()
        => this.WhenAnyValue(x => x.SelectedSubTab)
            .Subscribe(tab =>
            {
                this.RaisePropertyChanged(nameof(ShowConsole));
                this.RaisePropertyChanged(nameof(ShowMetrics));
                this.RaisePropertyChanged(nameof(ShowDatabase));
                this.RaisePropertyChanged(nameof(ShowAudit));
                this.RaisePropertyChanged(nameof(ShowPortView));
                this.RaisePropertyChanged(nameof(ShowDesktopView));
                this.RaisePropertyChanged(nameof(ShowDisplayView));
                this.RaisePropertyChanged(nameof(ShowTcpInfo));
                if (tab is PortSubTabViewModel { IsHttp: true } portTab)
                {
                    // Restore the last URL the user/agent was at on this port; fall back to base URL.
                    var origin = PortOrigin(portTab.Url);
                    AddressBarUrl = origin is not null && _portUrls.TryGetValue(origin, out var saved)
                        ? saved
                        : portTab.Url;
                }
                else
                {
                    AddressBarUrl = null;
                }
                if (tab is PortSubTabViewModel selectedPort)
                    _ = selectedPort.ProbeAsync();
                if (tab is ConsoleSubTabViewModel
                    && Sidebar.SelectedWorkspace?.Id is { } consoleWorkspaceId
                    && Applications.SelectedApplication?.Name is { } consoleApplication)
                    _ = Console.LoadAsync(consoleWorkspaceId, consoleApplication);
                if (tab is AuditSubTabViewModel
                    && Sidebar.SelectedWorkspace?.Id is { } workspaceId
                    && Applications.SelectedApplication?.Name is { } application)
                    _ = Audit.LoadAsync(workspaceId, application);
                else
                    Audit.Deactivate();
                if (tab is DatabaseSubTabViewModel
                    && Sidebar.SelectedWorkspace?.Id is { } dbWorkspaceId
                    && Applications.SelectedApplication?.Name is { } dbApplication)
                    _ = Database.LoadAsync(dbWorkspaceId, dbApplication);
            });

    private void SubscribeTutorialSteps()
        => Tutorial.WhenAnyValue(t => t.CurrentStep)
            .Skip(1)
            .Where(_ => Tutorial.IsVisible)
            .Subscribe(_step => _ = ReloadWorkspaceBehindTutorialAsync());

    private static void SubscribeSelectedPortProbe(IObservable<PortSubTabViewModel?> selectedPortTab)
        => selectedPortTab
            .Select(CreatePortProbeTimer)
            .Switch()
            .Subscribe(pt => _ = pt.ProbeAsync());

    private IObservable<(string? WorkspaceId, string? Url)> CreateBrowserNavigation(
        IObservable<PortSubTabViewModel?> selectedPortTab)
    {
        // Emit a navigation event whenever workspace or sub-tab changes.
        var workspaceChanged = Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Select(ws => (WorkspaceId: ws?.Id, Url: (string?)null));

        var portOpenChanged = selectedPortTab
            .Select(CreatePortOpenNavigation)
            .Switch();

        return workspaceChanged.Merge(portOpenChanged).Merge(_addressNavigations);
    }

    private IObservable<(string? WorkspaceId, string? Url)> CreateBrowserTabNavigation()
        => this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(CreateTabNavigation)
            .Where(nav => nav.HasValue)
            .Select(nav => nav!.Value);

    private (string? WorkspaceId, string? Url)? CreateTabNavigation(SubTabViewModel? tab)
    {
        if (tab is null)
        {
            _lastSelectedHttpPortKey = null;
            return (Sidebar.SelectedWorkspace?.Id, null);
        }

        if (tab is not PortSubTabViewModel { IsHttp: true } pt)
        {
            return (Sidebar.SelectedWorkspace?.Id, null);
        }

        var workspaceId = Sidebar.SelectedWorkspace?.Id;
        var portKey = $"{workspaceId}:{pt.AllocatedPort}";
        if (string.Equals(_lastSelectedHttpPortKey, portKey, StringComparison.Ordinal))
            return null;

        _lastSelectedHttpPortKey = portKey;
        return (workspaceId, GetPortNavigationUrl(pt));
    }

    private static IObservable<PortSubTabViewModel> CreatePortProbeTimer(PortSubTabViewModel? pt)
        => pt is null
            ? Observable.Empty<PortSubTabViewModel>()
            : Observable.Timer(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), RxApp.TaskpoolScheduler)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(_ => pt);

    private IObservable<(string?, string?)> CreatePortOpenNavigation(PortSubTabViewModel? pt)
        => pt is null
            ? Observable.Empty<(string?, string?)>()
            : pt.WhenAnyValue(t => t.IsOpen)
                .Skip(1)
                .Where(open => open)
                .Select(_ => ((string?)Sidebar.SelectedWorkspace?.Id, (string?)GetPortNavigationUrl(pt)));

    private string GetPortNavigationUrl(PortSubTabViewModel pt)
    {
        if (!pt.IsHttp) return pt.Url;
        var address = AddressBarUrl;
        return address is not null
               && (address.StartsWith("http://", StringComparison.Ordinal)
                   || address.StartsWith("https://", StringComparison.Ordinal))
            ? address
            : pt.Url;
    }

    private async Task ReloadWorkspaceBehindTutorialAsync()
    {
        await Sidebar.LoadAsync();
        _browserCommands.OnNext(BrowserCommand.Reload);
    }

    private void NavigateAddress()
    {
        if (SelectedSubTab is not PortSubTabViewModel { IsHttp: true }) return;
        if (string.IsNullOrWhiteSpace(AddressBarUrl)) return;

        var normalized = NormalizeAddress(AddressBarUrl);
        AddressBarUrl = normalized;
        _addressNavigations.OnNext((Sidebar.SelectedWorkspace?.Id, normalized));
    }

    internal void UpdateAddressFromBrowser(string workspaceId, string url)
    {
        if (Sidebar.SelectedWorkspace?.Id != workspaceId) return;
        if (!ShowPortView) return;
        // Only update the address bar if the URL belongs to the currently visible port tab.
        // Without this guard the 500ms address-poll timer overwrites the bar with a URL from
        // a different app (e.g. the user switched the Desktop tab to another port).
        if (SelectedSubTab is not PortSubTabViewModel { IsHttp: true } currentTab) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var incomingUri)) return;
        if (incomingUri.Port != currentTab.AllocatedPort) return;

        AddressBarUrl = url;

        var origin = PortOrigin(url);
        if (origin is not null)
            _portUrls[origin] = url;
    }

    // Registers a URL as the intended destination for its port before a tab switch occurs,
    // so SubscribeSubTabSelection restores the new URL rather than the previous one.
    internal void PreloadPortUrl(string url)
    {
        var origin = PortOrigin(url);
        if (origin is not null)
            _portUrls[origin] = url;
    }

    internal string? GetApplicationHttpOrigin(string workspaceId, string applicationName) =>
        ResolveApplicationOrigin(workspaceId, applicationName);

    string? IValidationReplayHost.ResolveApplicationOrigin(string workspaceId, string application) =>
        ResolveApplicationOrigin(workspaceId, application);

    private string? ResolveApplicationOrigin(string workspaceId, string applicationName)
    {
        var workspace = Sidebar.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        var app = workspace?.Applications.FirstOrDefault(a => string.Equals(a.Name, applicationName, StringComparison.Ordinal));
        var httpPort = app?.AllocatedPorts.FirstOrDefault(p =>
            string.Equals(p.Protocol, "http", StringComparison.OrdinalIgnoreCase));
        return httpPort is null ? null : $"http://127.0.0.1:{httpPort.AllocatedPort}";
    }

    internal void NavigateBrowserTo(string workspaceId, string url)
        => _addressNavigations.OnNext((workspaceId, url));

    Task IValidationReplayHost.NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken)
    {
        NavigateBrowserTo(workspaceId, url);
        return Task.CompletedTask;
    }

    Task<bool> IValidationReplayHost.PrepareViewportAsync(
        string workspaceId,
        string applicationName,
        string url,
        CancellationToken cancellationToken)
        => PrepareValidationViewportAsync(workspaceId, applicationName, url, cancellationToken);

    Task<string?> IValidationReplayHost.EvalAsync(string workspaceId, string script, CancellationToken cancellationToken)
        => _validationEvalAsync?.Invoke(workspaceId, script) ?? Task.FromResult<string?>(null);

    Task IValidationReplayHost.DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

    internal void ConnectValidationReplay(Browser.Controllers.BrowserViewportController viewport, IValidationReplayConnector replay)
    {
        _validationEvalAsync = viewport.EvalAsync;
        replay.Connect(this);
    }

    internal async Task<bool> PrepareValidationViewportAsync(
        string workspaceId,
        string applicationName,
        string url,
        CancellationToken cancellationToken)
    {
        var workspace = Sidebar.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace is null)
            return false;

        if (Sidebar.SelectedWorkspace?.Id != workspaceId)
            Sidebar.SelectedWorkspace = workspace;

        SelectedShellTab = WorkspaceShellTab.Application;

        var matchingApp = Applications.Applications
            .FirstOrDefault(a => string.Equals(a.Name, applicationName, StringComparison.Ordinal));
        if (matchingApp is not null && Applications.SelectedApplication != matchingApp)
            Applications.SelectedApplication = matchingApp;

        SelectApplicationForUrl(workspaceId, url);
        NavigateBrowserTo(workspaceId, url);
        await Task.Delay(750, cancellationToken);
        return true;
    }

    internal bool SelectApplicationForUrl(string workspaceId, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var workspace = Sidebar.Workspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (workspace is null)
            return false;

        var targetPort = uri.Port;
        var matchingWorkspaceApp = workspace.Applications
            .FirstOrDefault(a => a.AllocatedPorts.Any(p => p.AllocatedPort == targetPort));
        if (matchingWorkspaceApp is null)
            return false;

        if (Sidebar.SelectedWorkspace?.Id != workspaceId)
            return true;

        var matchingApp = Applications.Applications
            .FirstOrDefault(a => string.Equals(a.Name, matchingWorkspaceApp.Name, StringComparison.Ordinal));
        if (matchingApp is null)
            return false;

        // Pre-seed _lastSelectedHttpPortKey so the reactive tab-navigation observer does not
        // emit a redundant browser navigate when SelectedSubTab changes below.
        _lastSelectedHttpPortKey = $"{workspaceId}:{targetPort}";
        PreloadPortUrl(url);

        if (Applications.SelectedApplication != matchingApp)
            Applications.SelectedApplication = matchingApp;

        SelectedShellTab = WorkspaceShellTab.Application;

        var targetTab = SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault(t => t.AllocatedPort == targetPort);
        if (targetTab is not null && SelectedSubTab != targetTab)
            SelectedSubTab = targetTab;

        return true;
    }

    private static string? PortOrigin(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

    private static string NormalizeAddress(string address)
    {
        var trimmed = address.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : $"http://{trimmed}";
    }

    private void RebuildSubTabs(ApplicationViewModel? app)
    {
        SubTabs.Clear();
        if (app is null)
        {
            SelectedSubTab = null;
            return;
        }

        var ports = app.AllocatedPorts
            .Select(port => new PortTabRequest(port.Variable ?? string.Empty, port.DefaultPort, port.AllocatedPort, port.Protocol))
            .ToList();
        if (app.IsDesktop)
            SubTabs.Add(new DesktopSubTabViewModel());
        if (app.Database)
            SubTabs.Add(new DatabaseSubTabViewModel());
        foreach (var tab in _ports.CreateTabs(ports))
            SubTabs.Add(tab);
        SubTabs.Add(new AuditSubTabViewModel());

        SelectedSubTab = SubTabs.OfType<DesktopSubTabViewModel>().FirstOrDefault()
            ?? SubTabs.OfType<DatabaseSubTabViewModel>().FirstOrDefault()
            ?? SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault()
            ?? (SubTabViewModel)SubTabs[0];

        foreach (var portTab in SubTabs.OfType<PortSubTabViewModel>())
            _ = portTab.ProbeAsync();

        var wsApp = Sidebar.SelectedWorkspace?.Applications
            .FirstOrDefault(a => string.Equals(a.Name, app.Name, StringComparison.Ordinal));
        if (wsApp is not null)
            ApplyPortHealthToSubTabs(wsApp);
    }

    private void ApplyPortHealthToSubTabs(WorkspaceApplicationViewModel app)
    {
        var byPort = app.PortHealth?.ToDictionary(p => p.AllocatedPort, p => p.HealthState) ?? [];
        foreach (var tab in SubTabs.OfType<PortSubTabViewModel>())
        {
            var ledState = byPort.TryGetValue(tab.AllocatedPort, out var hs)
                ? hs switch
                {
                    "Healthy"   => PortLedState.Healthy,
                    "Checking"  => PortLedState.Checking,
                    "Unhealthy" => PortLedState.Unhealthy,
                    _           => PortLedState.Probing
                }
                : PortLedState.Probing;
            tab.SetLedState(ledState);
        }
    }

    private static ApplicationViewModel CreateApplicationViewModel(WorkspaceApplicationViewModel app) =>
        new(app.Name, app.Command, app.State, app.AllocatedPorts, app.Database, app.IsDesktop);

    public async Task InitializeAsync()
    {
        await Tutorial.InitializeAsync();
        await Sidebar.LoadAsync();
    }


    private void LoadValidation()
    {
        if (_validationController is null || Validation is null)
            return;

        // Selections change faster than the server answers. Without superseding the previous
        // load, a slow earlier response can land last and repaint the panel with another
        // application's flows. The old source is cancelled but not disposed: the request it
        // still owns would fault on a disposed token.
        var cts = new CancellationTokenSource();
        var previous = _validationLoad;
        _validationLoad = cts;
        previous?.Cancel();

        if (Sidebar.SelectedWorkspace?.Id is not { } workspaceId || Applications.SelectedApplication?.Name is not { } application)
        {
            Validation.Clear();
            return;
        }

        Validation.BeginLoad();
        _ = _validationController.LoadAsync(workspaceId, application, cts.Token);
    }

    private void UpdateChromeLeftItems(bool loginVisible)
        => Chrome.SetLeftItems(loginVisible ? [] : CreateWorkspaceChromeItems());

    private IEnumerable<object> CreateWorkspaceChromeItems()
    {
        yield return new ChromeIconButtonViewModel(
            "ReloadButton",
            "↺",
            15,
            Sidebar.RefreshCommand,
            "Reload workspaces");
        yield return _chromeServerStatus;
    }

    private void SyncShellTabItem()
    {
        var item = SelectedShellTab is WorkspaceShellTab.Application
            ? null
            : ShellTabs.FirstOrDefault(tab => tab.Kind == SelectedShellTab);
        this.RaiseAndSetIfChanged(ref _selectedShellTabItem, item, nameof(SelectedShellTabItem));
    }

    private void RaiseShellVisibility()
    {
        this.RaisePropertyChanged(nameof(ShowOverview));
        this.RaisePropertyChanged(nameof(ShowAgent));
        this.RaisePropertyChanged(nameof(ShowCommit));
        this.RaisePropertyChanged(nameof(ShowApplication));
        this.RaisePropertyChanged(nameof(ShowApplicationChrome));
        this.RaisePropertyChanged(nameof(ShowNoApplications));
        this.RaisePropertyChanged(nameof(IsValidationOpen));
        this.RaisePropertyChanged(nameof(SelectedApplicationTab));
        this.RaisePropertyChanged(nameof(ShowConsole));
        this.RaisePropertyChanged(nameof(ShowMetrics));
        this.RaisePropertyChanged(nameof(ShowDatabase));
        this.RaisePropertyChanged(nameof(ShowAudit));
        this.RaisePropertyChanged(nameof(ShowPortView));
        this.RaisePropertyChanged(nameof(ShowDesktopView));
        this.RaisePropertyChanged(nameof(ShowDisplayView));
        this.RaisePropertyChanged(nameof(ShowTcpInfo));
    }
}
