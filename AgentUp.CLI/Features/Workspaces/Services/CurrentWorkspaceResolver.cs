using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Workspaces.Services;

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
        var workspaceRoot = WorkspaceRootProvider.Find(_workingDirectory);
        if (workspaceRoot is null)
            return WorkspaceResolution.Failed("Error: agent-up.json not found in the current directory or any parent directory.");

        List<WorkspaceDto> workspaces;
        try
        {
            workspaces = await _client.ListAsync();
        }
        catch (AuthenticationRequiredException ex)
        {
            return WorkspaceResolution.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return WorkspaceResolution.Failed($"{queryFailureMessage}: {ex.Message}");
        }

        var workspace = workspaces.FirstOrDefault(w =>
            string.Equals(w.WorktreePath, workspaceRoot, StringComparison.OrdinalIgnoreCase));

        return workspace is null
            ? WorkspaceResolution.Failed(missingWorkspaceMessage)
            : WorkspaceResolution.Found(workspace);
    }
}
