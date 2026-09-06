using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Providers;

namespace AgentUp.Desktop.Features.Audit.Services;

public sealed class ApplicationAuditService(ApplicationAuditApiClient client)
{
    public Task<ApplicationAuditPageDto> GetPageAsync(
        string workspaceId, string application, DateTimeOffset? before, string? beforeEventId, int limit, CancellationToken cancellationToken)
        => client.GetPageAsync(workspaceId, application, before, beforeEventId, limit, cancellationToken);
}
