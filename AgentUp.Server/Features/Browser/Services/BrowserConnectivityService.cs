using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserConnectivityService(BrowserEventBus eventBus, ILogger<BrowserConnectivityService> logger)
    : IDisposable
{
    private const int MaxAttempts = 30;
    private const int RetryIntervalMs = 2000;
    private const int HealthCheckIntervalMs = 5000;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _probes = new();
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        DefaultRequestHeaders = { { "Accept", "*/*" } }
    };

    public void StartProbe(string workspaceId, string url, CancellationToken serverStopped)
    {
        if (_probes.TryRemove(workspaceId, out var old))
        {
            old.Cancel();
            old.Dispose();
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(serverStopped);
        _probes[workspaceId] = cts;
        _ = Task.Run(() => RunAsync(workspaceId, url, cts.Token), cts.Token);
    }

    public void StopProbe(string workspaceId)
    {
        if (_probes.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var cts in _probes.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _probes.Clear();
        _http.Dispose();
    }

    private async Task RunAsync(string workspaceId, string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var connected = await RetryUntilConnectedAsync(workspaceId, url, ct);
            if (!connected || ct.IsCancellationRequested) return;

            eventBus.PublishConnectivity(workspaceId, "connected", MaxAttempts, MaxAttempts);
            var healthy = await RunHealthCheckAsync(workspaceId, url, ct);
            if (!healthy && !ct.IsCancellationRequested)
                logger.LogDebug("Health check failed for {WorkspaceId} at {Url}; re-entering retry loop.", workspaceId, url);
        }
    }

    private async Task<bool> RetryUntilConnectedAsync(string workspaceId, string url, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            eventBus.PublishConnectivity(workspaceId, "connecting", attempt, MaxAttempts);
            if (await ProbeAsync(url, ct)) return true;
            if (attempt < MaxAttempts)
                await DelayAsync(RetryIntervalMs, ct);
        }
        if (!ct.IsCancellationRequested)
            eventBus.PublishConnectivity(workspaceId, "failed", MaxAttempts, MaxAttempts);
        return false;
    }

    private async Task<bool> RunHealthCheckAsync(string workspaceId, string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await DelayAsync(HealthCheckIntervalMs, ct);
            if (ct.IsCancellationRequested) return true;
            if (!await ProbeAsync(url, ct)) return false;
        }
        return true;
    }

    private async Task<bool> ProbeAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return (int)response.StatusCode < 500;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task DelayAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
    }
}
