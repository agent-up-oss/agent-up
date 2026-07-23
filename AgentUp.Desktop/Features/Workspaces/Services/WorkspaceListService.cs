using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;

namespace AgentUp.Desktop.Features.Workspaces.Services;

public sealed class WorkspaceListService
{
    private readonly IWorkspaceApiProvider _client;

    public WorkspaceListService(IWorkspaceApiProvider client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _client.ListAsync(cancellationToken);

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
        => await _client.CleanupTutorialWorkspacesAsync(cancellationToken);
}
