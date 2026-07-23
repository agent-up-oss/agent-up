using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Factories;

public static class CliRunnerFactory
{
    public static WorkspacesController Create(string serverUrl, string workingDirectory, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var client = new WorkspaceApiClient(http);
        var resolver = new CurrentWorkspaceResolver(client, workingDirectory);
        var service = new WorkspaceCommandService(
            client,
            new WorkspaceConfigurationProvider(),
            new WorkspaceIdentityProvider(),
            resolver,
            workingDirectory);

        return new WorkspacesController(
            serverUrl,
            writer,
            new StartCommand(service, writer),
            new StopCommand(service, writer),
            new ListCommand(service, writer),
            new StatusCommand(service, writer));
    }
}
