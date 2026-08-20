using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class NullAuditEventRepository : IAuditEventRepository
{
    public Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    public Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
        => Task.FromResult<AuditEvent?>(null);
}
