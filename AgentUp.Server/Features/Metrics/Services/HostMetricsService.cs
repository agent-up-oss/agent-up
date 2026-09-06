using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Metrics.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Metrics.Services;

public sealed class HostMetricsService(
    AuditController audit,
    ILogger<HostMetricsService> logger) : BackgroundService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CpuSampleWindow = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var metrics = await ProcessMetricsSampler.SampleAsync(CpuSampleWindow, stoppingToken);
                await audit.RecordAsync(new AuditRecordRequest(
                    Kind: "metrics",
                    Source: "server",
                    Action: "host_metrics_sample",
                    Outcome: "success",
                    WorkspaceId: null,
                    Details: metrics,
                    Scope: AuditScope.HostServer), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                logger.LogDebug(ex, "Host metrics sample failed");
            }

            try
            {
                await Task.Delay(SampleInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
