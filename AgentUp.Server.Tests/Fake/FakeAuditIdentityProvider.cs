using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeAuditIdentityProvider : IAuditIdentityProvider
{
    public AuditIdentity Identity { get; set; } = new(
        "/repo",
        "/repo/worktree",
        "workdir",
        "main",
        "abc123",
        false);

    public Task<AuditIdentity> ReadAsync(Workspace workspace, CancellationToken cancellationToken)
        => Task.FromResult(Identity);
}
