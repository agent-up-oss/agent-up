using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Audit.Interfaces;

public interface IAuditIdentityProvider
{
    Task<AuditIdentity> ReadAsync(Workspace workspace, CancellationToken cancellationToken);
}
