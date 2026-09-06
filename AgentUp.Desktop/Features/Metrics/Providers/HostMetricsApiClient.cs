using System.Net.Http.Json;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Metrics.Providers;

public sealed class HostMetricsApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<bool> RecordHostMetricsAsync(
        IReadOnlyDictionary<string, string> metrics,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            kind = "metrics",
            source = "desktop",
            action = "host_metrics_sample",
            outcome = "success",
            scope = "host-desktop",
            details = metrics
        };
        using var response = await http.PostAsJsonAsync("/api/audit/record", payload, Options, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
