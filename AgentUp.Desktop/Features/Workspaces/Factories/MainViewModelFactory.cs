using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Console.ViewModels;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Workspaces.Factories;

public static class MainViewModelFactory
{
    public static MainViewModel Create(
        WorkspaceApiClient workspaceClient,
        ConsoleApiClient consoleClient,
        FirstRunTutorialViewModel? tutorial = null)
        => new(
            new WorkspaceListViewModel(workspaceClient),
            new ApplicationListViewModel(),
            new ConsoleViewModel(consoleClient),
            tutorial ?? new FirstRunTutorialViewModel(
                new FileFirstRunTutorialSettingsStore(),
                new FirstRunTutorialChecks(workspaceClient)));

    public static MainViewModel Create(string serverUrl)
    {
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        return Create(new WorkspaceApiClient(http), new ConsoleApiClient(http));
    }
}
