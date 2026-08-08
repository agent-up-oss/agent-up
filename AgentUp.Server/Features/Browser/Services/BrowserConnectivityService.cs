using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserConnectivityService(
    BrowserEventBus eventBus,
    AppHealthController healthChecks,
    WorkspaceQueryController workspaceQuery,
    ILogger<BrowserConnectivityService> logger)
    : IDisposable
{
    private const int MaxAttempts = 30;
    private const int RetryIntervalMs = 2000;
    private const int HealthCheckIntervalMs = 5000;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _probes = new();
    private readonly ConcurrentDictionary<string, (string AppName, int Port, Action<string, string, int, bool> Handler)> _watches = new();
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        DefaultRequestHeaders = { { "Accept", "*/*" } }
    };

    public void StartProbe(string workspaceId, string url, CancellationToken serverStopped)
    {
        if (TryResolveHealthCheckedPort(workspaceId, url, out var appName, out var port))
            WatchPort(workspaceId, appName!, port, serverStopped);
        else
            RunStandaloneProbe(workspaceId, url, serverStopped);
    }

    public void StopProbe(string workspaceId)
    {
        if (_probes.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_watches.TryRemove(workspaceId, out var watch))
            healthChecks.PortHealthChanged -= watch.Handler;
    }

    public void Dispose()
    {
        foreach (var cts in _probes.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _probes.Clear();

        foreach (var watch in _watches.Values)
            healthChecks.PortHealthChanged -= watch.Handler;
        _watches.Clear();

        _http.Dispose();
    }

    private bool TryResolveHealthCheckedPort(string workspaceId, string url, out string? appName, out int port)
    {
        appName = null;
        port = 0;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var workspace = workspaceQuery.GetById(workspaceId);
        if (workspace is null) return false;

        var match = workspace.Applications
            .SelectMany(a => a.AllocatedPorts
                .Where(p => p.AllocatedPort == uri.Port && p.HealthCheckPath is not null)
                .Select(p => (AppName: a.Name, Port: p.AllocatedPort)))
            .FirstOrDefault();

        if (match == default) return false;
        appName = match.AppName;
        port = match.Port;
        return true;
    }

    private void WatchPort(string workspaceId, string appName, int port, CancellationToken serverStopped)
    {
        if (_watches.TryRemove(workspaceId, out var old))
            healthChecks.PortHealthChanged -= old.Handler;

        var currentHealth = healthChecks.GetPortHealth(workspaceId, appName);
        var currentState = currentHealth?.FirstOrDefault(p => p.AllocatedPort == port)?.HealthState;
        PublishConnectivityFromHealthState(workspaceId, currentState);

        void Handler(string wsId, string app, int p, bool isHealthy)
        {
            if (wsId != workspaceId || app != appName || p != port) return;
            if (serverStopped.IsCancellationRequested) return;
            PublishConnectivityFromHealthState(workspaceId, isHealthy ? "Healthy" : "Unhealthy");
        }

        healthChecks.PortHealthChanged += Handler;
        _watches[workspaceId] = (appName, port, Handler);

        serverStopped.Register(() =>
        {
            if (_watches.TryRemove(workspaceId, out var w))
                healthChecks.PortHealthChanged -= w.Handler;
        });
    }

    private void PublishConnectivityFromHealthState(string workspaceId, string? healthState)
    {
        switch (healthState)
        {
            case "Healthy":
                eventBus.PublishConnectivity(workspaceId, "connected", MaxAttempts, MaxAttempts);
                break;
            default:
                eventBus.PublishConnectivity(workspaceId, "connecting", 1, MaxAttempts);
                break;
        }
    }

    private void RunStandaloneProbe(string workspaceId, string url, CancellationToken serverStopped)
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
            if (await ProbeAsync(url, ct)) return true;
            eventBus.PublishConnectivity(workspaceId, "connecting", attempt, MaxAttempts);
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
