using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Services;

namespace AgentUp.Desktop.Features.Audit.Controllers;

public sealed class ApplicationAuditController(ApplicationAuditService service)
{
    public Task<ApplicationAuditPageDto> GetPageAsync(
        string workspaceId, string application, DateTimeOffset? before, int limit, CancellationToken cancellationToken)
        => service.GetPageAsync(workspaceId, application, before, limit, cancellationToken);
}
