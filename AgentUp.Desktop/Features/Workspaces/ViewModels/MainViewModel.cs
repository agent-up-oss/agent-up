using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.Controllers;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject
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

    public WorkspaceListViewModel Sidebar { get; }
    public ApplicationListViewModel Applications { get; }
    public ConsoleViewModel Console { get; }
    public FirstRunTutorialViewModel Tutorial { get; }

    public ObservableCollection<SubTabViewModel> SubTabs { get; } = [];

    public SubTabViewModel? SelectedSubTab
    {
        get => _selectedSubTab;
        set => this.RaiseAndSetIfChanged(ref _selectedSubTab, value);
    }

    public bool ShowConsole => SelectedSubTab is ConsoleSubTabViewModel;
    public bool ShowPortView => SelectedSubTab is PortSubTabViewModel { IsHttp: true };
    public bool ShowTcpInfo => SelectedSubTab is PortSubTabViewModel { IsHttp: false };

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
    public IObservable<BrowserCommand> BrowserCommands => _browserCommands;

    public MainViewModel(
        WorkspaceListViewModel sidebar,
        ApplicationListViewModel applications,
        ConsoleViewModel console,
        FirstRunTutorialViewModel tutorial,
        PortsController ports)
    {
        Sidebar = sidebar;
        Applications = applications;
        Console = console;
        Tutorial = tutorial;
        _ports = ports;

        NavigateAddressCommand = ReactiveCommand.Create(NavigateAddress);
        BrowserBackCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Back));
        BrowserForwardCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Forward));
        BrowserReloadCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Reload));

        var selectedPortTab = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab as PortSubTabViewModel);

        SubscribeWorkspaceSelection();
        SubscribeApplicationSelection();
        SubscribeSubTabSelection();
        SubscribeTutorialSteps();
        SubscribeSelectedPortProbe(selectedPortTab);

        BrowserNavigation = CreateBrowserNavigation(selectedPortTab);
    }

    private void SubscribeWorkspaceSelection()
        => Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Subscribe(ws =>
            {
                Console.Clear();
                SubscribeSelectedWorkspaceApplications(ws);
                UpdateApplicationsFromWorkspace(ws, preserveSelection: false);
            });

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
                if (app is null) return;
                var workspaceId = Sidebar.SelectedWorkspace?.Id;
                if (workspaceId is not null)
                    _ = Console.LoadAsync(workspaceId, app.Name);
            });

    private void SubscribeSubTabSelection()
        => this.WhenAnyValue(x => x.SelectedSubTab)
            .Subscribe(tab =>
            {
                this.RaisePropertyChanged(nameof(ShowConsole));
                this.RaisePropertyChanged(nameof(ShowPortView));
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

        var tabChanged = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(CreateTabNavigation);

        var portOpenChanged = selectedPortTab
            .Select(CreatePortOpenNavigation)
            .Switch();

        return workspaceChanged.Merge(tabChanged).Merge(portOpenChanged).Merge(_addressNavigations);
    }

    private (string? WorkspaceId, string? Url) CreateTabNavigation(SubTabViewModel? tab)
        => tab is PortSubTabViewModel { IsHttp: true } pt
            ? (Sidebar.SelectedWorkspace?.Id, GetPortNavigationUrl(pt))
            : (Sidebar.SelectedWorkspace?.Id, null);

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
        => pt.IsHttp ? AddressBarUrl ?? pt.Url : pt.Url;

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

        PreloadPortUrl(url);

        if (Applications.SelectedApplication != matchingApp)
            Applications.SelectedApplication = matchingApp;

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
        if (app is null) return;

        var ports = app.AllocatedPorts
            .Select(port => new PortTabRequest(port.Variable ?? string.Empty, port.DefaultPort, port.AllocatedPort, port.Protocol))
            .ToList();
        foreach (var tab in _ports.CreateTabs(ports))
            SubTabs.Add(tab);

        SelectedSubTab = SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault()
            ?? (SubTabViewModel)SubTabs[0];

        foreach (var portTab in SubTabs.OfType<PortSubTabViewModel>())
            _ = portTab.ProbeAsync();
    }

    private static ApplicationViewModel CreateApplicationViewModel(WorkspaceApplicationViewModel app) =>
        new(app.Name, app.Command, app.State, app.AllocatedPorts);

    public async Task InitializeAsync()
    {
        await Tutorial.InitializeAsync();
        await Sidebar.LoadAsync();
    }
}
