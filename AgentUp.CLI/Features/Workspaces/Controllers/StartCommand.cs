using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StartCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly WorkspaceCommandOutputService _output;

    public StartCommand(WorkspaceCommandService service, WorkspaceCommandOutputService output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
        => _output.WriteStartResult(await _service.StartAsync());
}
