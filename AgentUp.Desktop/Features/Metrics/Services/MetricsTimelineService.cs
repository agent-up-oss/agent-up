using AgentUp.Desktop.Features.Metrics.DTOs;
using AgentUp.Desktop.Features.Metrics.Providers;

namespace AgentUp.Desktop.Features.Metrics.Services;

public sealed class MetricsTimelineService(MetricsApiClient api)
{
    public Task<ApplicationMetricsTimelineDto?> GetTimelineAsync(
        string workspaceId,
        string appName,
        CancellationToken ct = default)
        => api.GetTimelineAsync(workspaceId, appName, ct: ct);
}
