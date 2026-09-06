using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Metrics.DTOs;

namespace AgentUp.Desktop.Features.Metrics.Providers;

public sealed class MetricsApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<ApplicationMetricsTimelineDto?> GetTimelineAsync(
        string workspaceId,
        string appName,
        int limit = 60,
        CancellationToken ct = default)
        => await http.GetFromJsonAsync<ApplicationMetricsTimelineDto>(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/metrics?limit={limit}",
            Options,
            ct);
}
