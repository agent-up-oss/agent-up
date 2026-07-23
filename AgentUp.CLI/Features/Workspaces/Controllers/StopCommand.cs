using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StopCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly TextWriter _output;

    public StopCommand(WorkspaceCommandService service, TextWriter output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var result = await _service.StopCurrentAsync();
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        var workspace = result.Value!;
        _output.WriteLine($"Stopped workspace \"{workspace.DisplayName}\"");
        return 0;
    }
}
