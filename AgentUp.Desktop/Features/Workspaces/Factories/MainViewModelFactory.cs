using AgentUp.Desktop.Features.Applications.Controllers;
using AgentUp.Desktop.Features.Applications.Services;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.Controllers;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Console.Services;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Ports.Controllers;
using AgentUp.Desktop.Features.Ports.Services;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Factories;

public static class MainViewModelFactory
{
    public static MainViewModel Create(
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        FirstRunTutorialViewModel? tutorial = null)
    {
        var workspaces = new WorkspacesController(new WorkspaceListService(workspaceClient));
        var applications = new ApplicationsController(new ApplicationSelectionService());
        var console = new ConsoleController(new ConsoleOutputService(consoleClient));
        var ports = new PortsController(new PortTabService());

        return new MainViewModel(
            new WorkspaceListViewModel(workspaces),
            new ApplicationListViewModel(applications),
            new ConsoleViewModel(console),
            tutorial ?? new FirstRunTutorialViewModel(
                new FileFirstRunTutorialSettingsStore(),
                new FirstRunTutorialChecks(workspaces)),
            ports);
    }

    public static MainViewModel Create(string serverUrl)
    {
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        return Create(new WorkspaceApiClient(http), new ConsoleApiClient(http));
    }
}
