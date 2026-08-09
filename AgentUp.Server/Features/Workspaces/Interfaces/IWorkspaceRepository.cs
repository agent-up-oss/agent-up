using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Interfaces;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Workspace>> LoadAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken = default);
}
