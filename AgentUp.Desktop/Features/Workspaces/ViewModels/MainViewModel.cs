using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private SubTabViewModel? _selectedSubTab;
    private string? _addressBarUrl;
    private readonly Subject<(string? WorkspaceId, string? Url)> _addressNavigations = new();
    private readonly Subject<BrowserCommand> _browserCommands = new();

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
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        FirstRunTutorialViewModel? tutorial = null)
    {
        Sidebar = new WorkspaceListViewModel(workspaceClient);
        Applications = new ApplicationListViewModel();
        Console = new ConsoleViewModel(consoleClient);
        Tutorial = tutorial ?? new FirstRunTutorialViewModel(
            new FileFirstRunTutorialSettingsStore(),
            new FirstRunTutorialChecks(workspaceClient));

        NavigateAddressCommand = ReactiveCommand.Create(NavigateAddress);
        BrowserBackCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Back));
        BrowserForwardCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Forward));
        BrowserReloadCommand = ReactiveCommand.Create(() => _browserCommands.OnNext(BrowserCommand.Reload));

        Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Subscribe(ws =>
            {
                Console.Clear();
                Applications.Update(ws?.Applications ?? []);
            });

        Applications.WhenAnyValue(x => x.SelectedApplication)
            .Subscribe(app =>
            {
                RebuildSubTabs(app);
                if (app is null) return;
                var workspaceId = Sidebar.SelectedWorkspace?.Id;
                if (workspaceId is not null)
                    _ = Console.LoadAsync(workspaceId, app.Name);
            });

        this.WhenAnyValue(x => x.SelectedSubTab)
            .Subscribe(tab =>
            {
                this.RaisePropertyChanged(nameof(ShowConsole));
                this.RaisePropertyChanged(nameof(ShowPortView));
                this.RaisePropertyChanged(nameof(ShowTcpInfo));
                if (tab is PortSubTabViewModel portTab)
                {
                    if (portTab.IsHttp)
                        AddressBarUrl = portTab.Url;
                    else
                        AddressBarUrl = null;
                    _ = portTab.ProbeAsync();
                }
                else
                {
                    AddressBarUrl = null;
                }
            });

        // Emit a navigation event whenever workspace or sub-tab changes.
        var workspaceChanged = Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Select(ws => (WorkspaceId: ws?.Id, Url: (string?)null));

        var tabChanged = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab is PortSubTabViewModel { IsHttp: true } pt
                ? (WorkspaceId: Sidebar.SelectedWorkspace?.Id, Url: (string?)pt.Url)
                : (WorkspaceId: Sidebar.SelectedWorkspace?.Id, Url: (string?)null));

        var selectedPortTab = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab as PortSubTabViewModel);

        // Re-probe the selected port every 3 s so IsOpen stays current while the tab is visible.
        selectedPortTab
            .Select(pt => pt is null
                ? Observable.Empty<PortSubTabViewModel>()
                : Observable.Timer(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), RxApp.TaskpoolScheduler)
                      .ObserveOn(RxApp.MainThreadScheduler)
                      .Select(_ => pt))
            .Switch()
            .Subscribe(pt => _ = pt.ProbeAsync());

        // Re-navigate when IsOpen flips false → true on the selected port tab.
        // The initial probe fires ~50 ms after tab-select, so this also re-navigates the
        // WebView after GTK has had time to realize the native widget (fixing blank-on-first-load).
        // It also catches backend restarts while the user is watching the port tab.
        var portOpenChanged = selectedPortTab
            .Select(pt => pt is null
                ? Observable.Empty<(string?, string?)>()
                : pt.WhenAnyValue(t => t.IsOpen)
                      .Skip(1)
                      .Where(open => open)
                      .Select(_ => ((string?)Sidebar.SelectedWorkspace?.Id, (string?)(pt.IsHttp ? AddressBarUrl ?? pt.Url : pt.Url))))
            .Switch();

        BrowserNavigation = workspaceChanged.Merge(tabChanged).Merge(portOpenChanged).Merge(_addressNavigations);
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
    }

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

        foreach (var port in app.AllocatedPorts)
            SubTabs.Add(new PortSubTabViewModel(port.Variable, port.DefaultPort, port.AllocatedPort, port.Protocol));
        SubTabs.Add(new ConsoleSubTabViewModel());

        SelectedSubTab = SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault()
            ?? (SubTabViewModel)SubTabs[0];

        foreach (var portTab in SubTabs.OfType<PortSubTabViewModel>())
            _ = portTab.ProbeAsync();
    }

    public async Task InitializeAsync()
    {
        await Tutorial.InitializeAsync();
        await Sidebar.LoadAsync();
    }
}
