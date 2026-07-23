using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StatusCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly WorkspaceCommandOutputService _output;

    public StatusCommand(WorkspaceCommandService service, WorkspaceCommandOutputService output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
        => _output.WriteStatusResult(await _service.ResolveCurrentAsync(
            "Error: Failed to query workspaces",
            "No workspace registered for this directory."));
}
