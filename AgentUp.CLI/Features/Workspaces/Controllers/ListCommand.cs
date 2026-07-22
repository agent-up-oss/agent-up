using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class ListCommand
{
    private readonly WorkspaceCommandService _service;
    private readonly TextWriter _output;

    public ListCommand(WorkspaceCommandService service, TextWriter output)
    {
        _service = service;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var result = await _service.ListAsync();
        if (!result.Succeeded)
        {
            _output.WriteLine(result.Error);
            return 1;
        }

        var workspaces = result.Value!;
        if (workspaces.Count == 0)
        {
            _output.WriteLine("No workspaces registered.");
            return 0;
        }

        _output.WriteLine($"{"ID",-38} {"Name",-20} {"Branch",-24} State");
        _output.WriteLine(new string('-', 90));
        foreach (var w in workspaces)
            _output.WriteLine($"{w.Id,-38} {w.DisplayName,-20} {w.Branch,-24} {w.State}");

        return 0;
    }
}
