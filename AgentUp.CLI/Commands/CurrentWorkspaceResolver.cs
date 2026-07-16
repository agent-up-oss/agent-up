using AgentUp.CLI.Http;

namespace AgentUp.CLI.Commands;

public sealed class CurrentWorkspaceResolver
{
    private readonly WorkspaceApiClient _client;
    private readonly string _workingDirectory;

    public CurrentWorkspaceResolver(WorkspaceApiClient client, string workingDirectory)
    {
        _client = client;
        _workingDirectory = workingDirectory;
    }

    public async Task<WorkspaceResolution> ResolveAsync(string queryFailureMessage, string missingWorkspaceMessage)
    {
        List<WorkspaceDto> workspaces;
        try
        {
            workspaces = await _client.ListAsync();
        }
        catch (Exception ex)
        {
            return WorkspaceResolution.Failed($"{queryFailureMessage}: {ex.Message}");
        }

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(w.WorktreePath, _workingDirectory, StringComparison.OrdinalIgnoreCase));

        return workspace is null
            ? WorkspaceResolution.Failed(missingWorkspaceMessage)
            : WorkspaceResolution.Found(workspace);
    }
}
