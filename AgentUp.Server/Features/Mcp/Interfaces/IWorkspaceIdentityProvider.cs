using AgentUp.Server.Features.Mcp.DTOs;

namespace AgentUp.Server.Features.Mcp.Interfaces;

public interface IWorkspaceIdentityProvider
{
    Task<WorkspaceIdentity> ReadAsync(string worktreePath, CancellationToken cancellationToken);
}
