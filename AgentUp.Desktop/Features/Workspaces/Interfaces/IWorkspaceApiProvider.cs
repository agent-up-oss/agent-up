using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Features.Workspaces.Interfaces;

public interface IWorkspaceApiProvider
{
    Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default);
}
