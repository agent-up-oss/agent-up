using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class ClearCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly TextWriter _output;

    public ClearCommand(WorkspaceCommandService service, TextWriter output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var result = await _service.ClearAllAsync();
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        _output.WriteLine($"Cleared {result.Value} workspace(s).");
        return 0;
    }
}
