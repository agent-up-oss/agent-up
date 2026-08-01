using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Interfaces;

public interface IAuditEventRepository
{
    Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken);
    Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken);
}
