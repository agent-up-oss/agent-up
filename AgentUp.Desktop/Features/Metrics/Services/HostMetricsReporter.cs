using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Metrics.Providers;

namespace AgentUp.Desktop.Features.Metrics.Services;

// Reports Desktop process metrics to the Server audit trail (host-desktop scope).
public sealed class HostMetricsReporter : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CpuSampleWindow = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;

    public HostMetricsReporter(HttpClient http) => _http = http;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var metrics = await ProcessMetricsSampler.SampleAsync(CpuSampleWindow, ct);
                var payload = new
                {
                    kind = "metrics",
                    source = "desktop",
                    action = "host_metrics_sample",
                    outcome = "success",
                    scope = "host-desktop",
                    details = metrics
                };
                using var response = await _http.PostAsJsonAsync("/api/audit/record", payload, JsonOptions, ct);
                if (!response.IsSuccessStatusCode)
                    Trace.TraceWarning($"[HostMetricsReporter] Audit POST failed: {(int)response.StatusCode}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                Trace.TraceWarning($"[HostMetricsReporter] Sample failed: {ex.Message}");
            }

            try
            {
                await Task.Delay(SampleInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
