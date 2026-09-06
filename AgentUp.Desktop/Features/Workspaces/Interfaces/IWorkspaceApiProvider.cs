using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Features.Workspaces.Interfaces;

public interface IWorkspaceApiProvider
{
    Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task StartAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task StopAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default);
}
