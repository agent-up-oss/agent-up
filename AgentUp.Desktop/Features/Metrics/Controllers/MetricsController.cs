using AgentUp.Desktop.Features.Metrics.DTOs;
using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Features.Metrics.Controllers;

public sealed class MetricsController(MetricsTimelineService timeline)
{
    public Task<ApplicationMetricsTimelineDto?> GetTimelineAsync(
        string workspaceId,
        string appName,
        CancellationToken ct = default)
        => timeline.GetTimelineAsync(workspaceId, appName, ct);
}
