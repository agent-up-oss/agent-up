using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StatusCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly string _workingDirectory;
    private readonly TextWriter _output;

    public StatusCommand(WorkspaceApiClient client, string workingDirectory, TextWriter output)
    {
        _client = client;
        _workingDirectory = workingDirectory;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var resolution = await new CurrentWorkspaceResolver(_client, _workingDirectory).ResolveAsync(
            "Error: Failed to query workspaces",
            "No workspace registered for this directory.");
        if (!resolution.Succeeded)
        {
            _output.WriteLine(resolution.Error);
            return 1;
        }

        var workspace = resolution.Workspace!;
        _output.WriteLine($"Name:       {workspace.DisplayName}");
        _output.WriteLine($"Branch:     {workspace.Branch}");
        _output.WriteLine($"Commit:     {workspace.Commit}");
        _output.WriteLine($"Repository: {workspace.RepositoryPath}");
        _output.WriteLine($"Worktree:   {workspace.WorktreePath}");
        _output.WriteLine($"State:      {workspace.State}");
        return 0;
    }
}
