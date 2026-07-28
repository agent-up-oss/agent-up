using AgentUp.Server.Features.Orchestration.DTOs;

namespace AgentUp.Server.Features.Orchestration.Interfaces;

public interface IWorkspaceIdentityProvider
{
    Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken);
}
