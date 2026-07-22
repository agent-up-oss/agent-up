using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StatusCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly TextWriter _output;

    public StatusCommand(WorkspaceCommandService service, TextWriter output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var resolution = await _service.ResolveCurrentAsync(
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
