using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Factories;

public static class CliRunnerFactory
{
    public static CliRunner Create(string serverUrl, string workingDirectory, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var client = new WorkspaceApiClient(http);
        var resolver = new CurrentWorkspaceResolver(client, workingDirectory);

        return new CliRunner(
            serverUrl,
            writer,
            new StartCommand(
                client,
                new WorkspaceConfigurationProvider(),
                new WorkspaceIdentityProvider(),
                workingDirectory,
                writer),
            new StopCommand(client, resolver, writer),
            new ListCommand(client, writer),
            new StatusCommand(resolver, writer));
    }
}
