using System.Collections.ObjectModel;
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
    public bool ShowPortView => SelectedSubTab is PortSubTabViewModel;

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
                if (tab is PortSubTabViewModel portTab)
                    _ = portTab.ProbeAsync();
            });
    }

    private void RebuildSubTabs(ApplicationViewModel? app)
    {
        SubTabs.Clear();
        if (app is null) return;

        SubTabs.Add(new ConsoleSubTabViewModel());
        foreach (var port in app.AllocatedPorts)
            SubTabs.Add(new PortSubTabViewModel(port.Variable, port.DefaultPort, port.AllocatedPort));

        SelectedSubTab = SubTabs[0];

        foreach (var portTab in SubTabs.OfType<PortSubTabViewModel>())
            _ = portTab.ProbeAsync();
    }

    public async Task InitializeAsync() => await Sidebar.LoadAsync();
}
