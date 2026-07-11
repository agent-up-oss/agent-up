using AgentUp.CLI.Http;

namespace AgentUp.CLI.Commands;

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
        List<WorkspaceDto> workspaces;
        try
        {
            workspaces = await _client.ListAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: Failed to query workspaces: {ex.Message}");
            return 1;
        }

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(w.WorktreePath, _workingDirectory, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
        {
            _output.WriteLine("No workspace registered for this directory.");
            return 1;
        }

        _output.WriteLine($"Name:       {workspace.DisplayName}");
        _output.WriteLine($"Branch:     {workspace.Branch}");
        _output.WriteLine($"Commit:     {workspace.Commit}");
        _output.WriteLine($"Repository: {workspace.RepositoryPath}");
        _output.WriteLine($"Worktree:   {workspace.WorktreePath}");
        _output.WriteLine($"State:      {workspace.State}");
        return 0;
    }
}
