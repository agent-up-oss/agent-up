using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Diagnostics.Services;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

public sealed class WorkspaceDiagnosticsController(WorkspaceDiagnosticsService diagnostics)
{
    public Task<WorkspaceDiagnosticsDto?> GetAsync(
        string workspaceId,
        string? application,
        int logLimit,
        int entryLimit,
        CancellationToken cancellationToken)
        => diagnostics.GetAsync(workspaceId, application, logLimit, entryLimit, cancellationToken);
}
