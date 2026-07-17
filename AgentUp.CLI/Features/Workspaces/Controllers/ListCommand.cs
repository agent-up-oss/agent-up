using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class ListCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly TextWriter _output;

    public ListCommand(WorkspaceApiClient client, TextWriter output)
    {
        _client = client;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        List<WorkspaceDto> workspaces;
        try
        {
            workspaces = await _client.ListAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: Failed to list workspaces: {ex.Message}");
            return 1;
        }

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
