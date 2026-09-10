using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Providers;

namespace AgentUp.Desktop.Features.Audit.Services;

public sealed class ApplicationAuditService(ApplicationAuditApiClient client)
{
    public Task<ApplicationAuditPageDto> GetPageAsync(
        string workspaceId,
        string application,
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> streams,
        DateTimeOffset? before,
        string? beforeEventId,
        int limit,
        CancellationToken cancellationToken)
        => client.GetPageAsync(workspaceId, application, kinds, streams, before, beforeEventId, limit, cancellationToken);
}
