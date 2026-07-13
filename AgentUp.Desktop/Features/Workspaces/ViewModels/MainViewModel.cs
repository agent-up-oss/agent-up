using System.Collections.ObjectModel;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class MainViewModel : ReactiveObject
{
    private SubTabViewModel? _selectedSubTab;

    public WorkspaceListViewModel Sidebar { get; }
    public ApplicationListViewModel Applications { get; }
    public ConsoleViewModel Console { get; }

    public ObservableCollection<SubTabViewModel> SubTabs { get; } = [];

    public SubTabViewModel? SelectedSubTab
    {
        get => _selectedSubTab;
        set => this.RaiseAndSetIfChanged(ref _selectedSubTab, value);
    }

    public bool ShowConsole => SelectedSubTab is ConsoleSubTabViewModel;
    public bool ShowPortView => SelectedSubTab is PortSubTabViewModel { IsHttp: true };
    public bool ShowTcpInfo => SelectedSubTab is PortSubTabViewModel { IsHttp: false };

    // Emits (workspaceId, url) when the browser should navigate.
    // workspaceId drives which isolated session to use; url is the destination.
    public IObservable<(string? WorkspaceId, string? Url)> BrowserNavigation { get; }

    public MainViewModel(WorkspaceApiClient workspaceClient, ConsoleApiClient consoleClient)
    {
        Sidebar = new WorkspaceListViewModel(workspaceClient);
        Applications = new ApplicationListViewModel();
        Console = new ConsoleViewModel(consoleClient);

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
                    _ = portTab.ProbeAsync();
            });

        // Emit a navigation event whenever workspace or sub-tab changes.
        var workspaceChanged = Sidebar.WhenAnyValue(x => x.SelectedWorkspace)
            .Select(ws => (WorkspaceId: ws?.Id, Url: (string?)null));

        var tabChanged = this.WhenAnyValue(x => x.SelectedSubTab)
            .Select(tab => tab is PortSubTabViewModel { IsHttp: true } pt
                ? (WorkspaceId: Sidebar.SelectedWorkspace?.Id, Url: (string?)pt.Url)
                : (WorkspaceId: Sidebar.SelectedWorkspace?.Id, Url: (string?)null));

        BrowserNavigation = workspaceChanged.Merge(tabChanged);
    }

    private void RebuildSubTabs(ApplicationViewModel? app)
    {
        SubTabs.Clear();
        if (app is null) return;

        SubTabs.Add(new ConsoleSubTabViewModel());
        foreach (var port in app.AllocatedPorts)
            SubTabs.Add(new PortSubTabViewModel(port.Variable, port.DefaultPort, port.AllocatedPort, port.Protocol));

        SelectedSubTab = SubTabs[0];

        foreach (var portTab in SubTabs.OfType<PortSubTabViewModel>())
            _ = portTab.ProbeAsync();
    }

    public async Task InitializeAsync() => await Sidebar.LoadAsync();
}
