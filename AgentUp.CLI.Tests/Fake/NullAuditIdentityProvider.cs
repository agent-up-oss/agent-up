using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class NullAuditIdentityProvider : IAuditIdentityProvider
{
    public Task<AuditIdentity> ReadAsync(Workspace workspace, CancellationToken cancellationToken)
        => Task.FromResult(new AuditIdentity(null, null, null, null, null, null));
}
