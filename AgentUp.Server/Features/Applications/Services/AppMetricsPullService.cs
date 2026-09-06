using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.Providers;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Applications.Services;

public sealed class AppMetricsPullService(
    AuditController audit,
    ILogger<AppMetricsPullService> logger) : IDisposable
{
    private static readonly TimeSpan PullInterval = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _workspaceCts = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public void StartForWorkspace(Workspace workspace)
    {
        if (_workspaceCts.TryRemove(workspace.Id, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        var cts = new CancellationTokenSource();
        _workspaceCts[workspace.Id] = cts;

        foreach (var app in workspace.Applications)
        {
            foreach (var port in app.AllocatedPorts.Where(p => p.MetricsPath is not null))
            {
                _ = Task.Run(
                    () => RunPortPullAsync(workspace.Id, app.Name, port.AllocatedPort, port.MetricsPath!, cts.Token),
                    cts.Token);
            }
        }
    }

    public void StopForWorkspace(string workspaceId)
    {
        if (_workspaceCts.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var cts in _workspaceCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _workspaceCts.Clear();
        _http.Dispose();
    }

    private async Task RunPortPullAsync(
        string workspaceId,
        string appName,
        int allocatedPort,
        string path,
        CancellationToken ct)
    {
        var url = $"http://localhost:{allocatedPort}{path}";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PullInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            IReadOnlyDictionary<string, string> metrics;
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug(
                        "App metrics pull failed: {WorkspaceId}/{AppName}:{Port} → HTTP {StatusCode}",
                        SanitizeForLog(workspaceId),
                        SanitizeForLog(appName),
                        allocatedPort,
                        (int)response.StatusCode);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                metrics = MetricsResponseParser.Parse(body);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                logger.LogDebug(
                    ex,
                    "App metrics pull failed: {WorkspaceId}/{AppName}:{Port}",
                    SanitizeForLog(workspaceId),
                    SanitizeForLog(appName),
                    allocatedPort);
                continue;
            }

            if (metrics.Count == 0)
                continue;

            var details = new Dictionary<string, string>(metrics, StringComparer.Ordinal)
            {
                ["appName"] = appName,
                ["port"] = allocatedPort.ToString(),
                ["url"] = url
            };

            _ = audit.RecordAsync(new AuditRecordRequest(
                Kind: "metrics",
                Source: "server",
                Action: "app_metrics_pull",
                Outcome: "success",
                WorkspaceId: workspaceId,
                Details: details,
                Scope: AuditScope.Application), CancellationToken.None);
        }
    }

    private static string SanitizeForLog(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", string.Empty, StringComparison.Ordinal);
}
