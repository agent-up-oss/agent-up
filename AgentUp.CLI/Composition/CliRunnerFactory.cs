using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Providers;
using AgentUp.CLI.Features.Commits.Services;
using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Composition;

public static class CliRunnerFactory
{
    public static WorkspacesController Create(string serverUrl, string workingDirectory, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;

        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var client = new WorkspaceApiClient(http);
        var resolver = new CurrentWorkspaceResolver(client, workingDirectory);
        var workspaceService = new WorkspaceCommandService(
            client,
            new WorkspaceConfigurationProvider(),
            new WorkspaceIdentityProvider(),
            resolver,
            workingDirectory);
        var workspaceOutput = new WorkspaceCommandOutputService(writer);

        var commitsGit = new CommitsGitProvider(workingDirectory);
        var commitsQueue = new CommitsQueueProvider(commitsGit);
        var commitsService = new CommitsService(commitsQueue, commitsGit);
        var commitsOutput = new CommitsOutputService(writer);
        var commitsParser = new CommitsArgParser();
        var commitsFormatParser = new CommitsFormatParser();
        var commits = new CommitsController(
            new CommitsEnqueueCommand(commitsService, commitsParser, commitsOutput),
            new CommitsStatusCommand(commitsService, commitsOutput, commitsFormatParser),
            new CommitsNextCommand(commitsService, commitsOutput, commitsFormatParser),
            new CommitsClearCommand(commitsService, commitsOutput),
            commitsOutput);

        return new WorkspacesController(
            serverUrl,
            writer,
            new StartCommand(workspaceService, workspaceOutput),
            new StopCommand(workspaceService, writer),
            new ListCommand(workspaceService, workspaceOutput),
            new StatusCommand(workspaceService, workspaceOutput),
            commits);
    }
}
