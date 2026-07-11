using AgentUp.CLI.Http;

namespace AgentUp.CLI.Commands;

public sealed class StopCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly string _workingDirectory;
    private readonly TextWriter _output;

    public StopCommand(WorkspaceApiClient client, string workingDirectory, TextWriter output)
    {
        _client = client;
        _workingDirectory = workingDirectory;
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
            _output.WriteLine($"Error: Failed to connect to server: {ex.Message}");
            return 1;
        }

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(w.WorktreePath, _workingDirectory, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
        {
            _output.WriteLine("Error: No workspace found for the current directory. Run 'agent-up start' first.");
            return 1;
        }

        try
        {
            await _client.StopWorkspaceAsync(workspace.Id);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        _output.WriteLine($"Stopped workspace \"{workspace.DisplayName}\"");
        return 0;
    }
}
