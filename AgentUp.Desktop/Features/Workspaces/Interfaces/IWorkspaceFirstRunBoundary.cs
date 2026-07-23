using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Features.Workspaces.Interfaces;

public interface IWorkspaceFirstRunBoundary
{
    Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default);
}
