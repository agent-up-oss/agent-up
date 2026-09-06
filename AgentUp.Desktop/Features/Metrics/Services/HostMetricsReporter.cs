using System.Diagnostics;
using AgentUp.Desktop.Features.Metrics.Providers;

namespace AgentUp.Desktop.Features.Metrics.Services;

// Reports Desktop process metrics to the Server audit trail (host-desktop scope).
public sealed class HostMetricsReporter(HostMetricsApiClient apiClient) : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CpuSampleWindow = TimeSpan.FromMilliseconds(250);

    private CancellationTokenSource? _cts;

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
                var recorded = await apiClient.RecordHostMetricsAsync(metrics, ct);
                if (!recorded)
                    Trace.TraceWarning("[HostMetricsReporter] Audit POST failed.");
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
