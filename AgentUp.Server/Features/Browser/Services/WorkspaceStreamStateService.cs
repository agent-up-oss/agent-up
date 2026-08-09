using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

// Single source of truth for what state the workspace's AI viewer should show.
// All prior signals (chromium download, browser-connectivity retries, session
// liveness) collapse into one derived StreamState per workspace, published as a
// single SSE event kind. Cache is cleared on workspace stop/remove — no stale
// "connected" surviving a lifecycle transition.
public sealed class WorkspaceStreamStateService : IDisposable
{
    private const int StandaloneMaxAttempts = 30;
    private const int StandaloneRetryIntervalMs = 2000;

    private readonly BrowserEventBus _eventBus;
    private readonly AppHealthController _healthChecks;
    private readonly WorkspaceQueryController _workspaceQuery;
    private readonly ILogger<WorkspaceStreamStateService> _logger;

    private readonly Lock _lock = new();
    private (string State, int Progress) _chromium = ("not_started", 0);
    private readonly Dictionary<string, WorkspaceStreamInputs> _inputs = [];
    private readonly Dictionary<string, StreamState> _lastPublished = [];

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _standaloneProbes = new();
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        DefaultRequestHeaders = { { "Accept", "*/*" } }
    };

    public WorkspaceStreamStateService(
        BrowserEventBus eventBus,
        AppHealthController healthChecks,
        WorkspaceQueryController workspaceQuery,
        ILogger<WorkspaceStreamStateService> logger)
    {
        _eventBus = eventBus;
        _healthChecks = healthChecks;
        _workspaceQuery = workspaceQuery;
        _logger = logger;
        _healthChecks.PortHealthChanged += HandlePortHealthChanged;
    }

    public void Dispose()
    {
        _healthChecks.PortHealthChanged -= HandlePortHealthChanged;
        foreach (var cts in _standaloneProbes.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _standaloneProbes.Clear();
        _http.Dispose();
    }

    // ── Global signals ────────────────────────────────────────────────

    public void OnChromiumStateChanged(string state, int progress)
    {
        List<string> workspaces;
        lock (_lock)
        {
            _chromium = (state, progress);
            workspaces = [.. _inputs.Keys];
        }

        // Chromium is a global input — re-derive state for every known workspace.
        // If nobody has started a workspace yet, there's nothing to publish per-workspace;
        // the cached value will surface as soon as a workspace registers via OnWorkspaceStarted.
        foreach (var ws in workspaces) RecomputeAndPublish(ws);
    }

    // ── Per-workspace lifecycle ───────────────────────────────────────

    public void OnWorkspaceStarted(Workspace workspace)
    {
        lock (_lock)
        {
            // Merge, don't replace. A previous EnsureSessionAsync (from an early viewer
            // WebSocket reconnect) may have already set SessionActive=true — wiping it
            // strands the state at SessionLaunching forever because EnsureSessionAsync
            // fast-paths on the second navigate and never re-emits OnSessionActive.
            // CurrentTarget IS reset: ReallocatePortsAsync during Start may change the
            // port, so the desktop needs to re-navigate to refresh the target.
            var existing = _inputs.GetValueOrDefault(workspace.Id);
            _inputs[workspace.Id] = (existing ?? new WorkspaceStreamInputs()) with
            {
                IsRunning = true,
                PortHealth = new Dictionary<string, string>(),
                CurrentTarget = null,
            };
        }
        RecomputeAndPublish(workspace.Id);
    }

    public void OnWorkspaceStopped(string workspaceId)
    {
        CancelStandaloneProbe(workspaceId);
        lock (_lock)
        {
            _inputs[workspaceId] = _inputs.TryGetValue(workspaceId, out var current)
                ? current with
                {
                    IsRunning = false,
                    PortHealth = new Dictionary<string, string>(),
                    SessionActive = false,
                    CurrentTarget = null,
                }
                : new WorkspaceStreamInputs { IsRunning = false, PortHealth = new Dictionary<string, string>() };
        }
        RecomputeAndPublish(workspaceId);
    }

    public void OnWorkspaceRemoved(string workspaceId)
    {
        CancelStandaloneProbe(workspaceId);
        lock (_lock)
        {
            _inputs.Remove(workspaceId);
            _lastPublished.Remove(workspaceId);
        }
        _eventBus.RemoveWorkspaceStreamStateCache(workspaceId);
    }

    // ── Browser session signals ───────────────────────────────────────

    public void OnSessionActive(string workspaceId)
    {
        lock (_lock)
        {
            // Upsert: session may become active before OnWorkspaceStarted fires (e.g. viewer
            // HTML JS reconnects its WebSocket on server restart, triggering EnsureSessionAsync
            // before the user clicks Start). Buffer the signal so later start doesn't wipe it.
            var current = _inputs.GetValueOrDefault(workspaceId) ?? new WorkspaceStreamInputs();
            _inputs[workspaceId] = current with { SessionActive = true };
        }
        RecomputeAndPublish(workspaceId);
    }

    public void OnSessionInactive(string workspaceId)
    {
        lock (_lock)
        {
            var current = _inputs.GetValueOrDefault(workspaceId) ?? new WorkspaceStreamInputs();
            _inputs[workspaceId] = current with { SessionActive = false };
        }
        RecomputeAndPublish(workspaceId);
    }

    // ── Navigation target (replaces BrowserConnectivityService.StartProbe) ─

    public void OnCurrentTargetChanged(string workspaceId, string url, CancellationToken serverStopped)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var (appName, port, healthChecked) = ResolveTarget(workspaceId, uri);

        lock (_lock)
        {
            // Upsert: same reasoning as OnSessionActive — navigate may reach us before
            // OnWorkspaceStarted.
            var current = _inputs.GetValueOrDefault(workspaceId) ?? new WorkspaceStreamInputs();
            var previous = current.CurrentTarget;
            if (previous is not null && previous.Port == uri.Port && previous.Url == url) return;
            _inputs[workspaceId] = current with
            {
                CurrentTarget = new CurrentStreamTarget(appName, uri.Port, url, healthChecked),
            };
        }

        if (!healthChecked) StartStandaloneProbe(workspaceId, url, serverStopped);
        else CancelStandaloneProbe(workspaceId);

        RecomputeAndPublish(workspaceId);
    }

    // ── AppHealthController → per-port health ─────────────────────────

    private void HandlePortHealthChanged(string workspaceId, string appName, int port, bool isHealthy)
    {
        var key = HealthKey(appName, port);
        var state = isHealthy ? "Healthy" : "Unhealthy";

        lock (_lock)
        {
            if (!_inputs.TryGetValue(workspaceId, out var current)) return;
            var updated = new Dictionary<string, string>(current.PortHealth) { [key] = state };
            _inputs[workspaceId] = current with { PortHealth = updated };
        }
        RecomputeAndPublish(workspaceId);
    }

    // ── Derivation & publication ──────────────────────────────────────

    private void RecomputeAndPublish(string workspaceId)
    {
        StreamState next;
        StreamState? previous;
        lock (_lock)
        {
            // Removed workspaces publish one final WorkspaceStopped so any live UI clears.
            next = _inputs.TryGetValue(workspaceId, out var inputs)
                ? Compute(inputs)
                : StreamState.Stopped();
            _lastPublished.TryGetValue(workspaceId, out previous);
            _lastPublished[workspaceId] = next;
        }

        if (previous is not null && previous == next) return;
        _eventBus.PublishStreamState(workspaceId, next);
    }

    private StreamState Compute(WorkspaceStreamInputs inputs)
    {
        // 1. Chromium precedence: if the binary isn't ready, nothing else matters.
        var (chromiumState, chromiumProgress) = _chromium;
        if (chromiumState is "not_started" or "downloading" or "failed")
            return StreamState.Chromium(chromiumState, chromiumProgress);

        // 2. Workspace lifecycle.
        if (!inputs.IsRunning) return StreamState.Stopped();

        // 3. App reachability. If the current target has a health check, drive from that.
        //    Otherwise the standalone probe loop publishes AppConnecting/AppFailed directly.
        if (inputs.CurrentTarget is { HealthChecked: true, AppName: { } appName } target)
        {
            var key = HealthKey(appName, target.Port);
            var state = inputs.PortHealth.GetValueOrDefault(key, "Checking");
            if (state != "Healthy") return StreamState.Connecting(attempt: 0, maxAttempts: 0);
        }
        else if (inputs.CurrentTarget is null)
        {
            // Waiting for the desktop to navigate to a URL. Show a benign "connecting" so
            // the UI never renders a bare WebView while we have no target.
            return StreamState.Connecting(attempt: 0, maxAttempts: 0);
        }

        // 4. Session liveness. Session must exist server-side — otherwise the viewer HTML
        //    page opens a WebSocket to nothing. First-frame liveness is intentionally NOT
        //    tracked here: the RDP display loop only broadcasts frames when a subscriber
        //    exists, and the viewer HTML page only subscribes once the desktop shows the
        //    WebView. Gating Streaming on "first frame received" would deadlock. The viewer
        //    HTML has its own "connecting…" spinner shown until the first frame lands, so
        //    the "bare WebView" invariant is upheld by the viewer page itself, not by us.
        if (!inputs.SessionActive) return StreamState.Launching();

        return StreamState.Streaming();
    }

    // ── Standalone probe (non-health-checked ports) ───────────────────

    private (string? AppName, int Port, bool HealthChecked) ResolveTarget(string workspaceId, Uri uri)
    {
        var workspace = _workspaceQuery.GetById(workspaceId);
        if (workspace is null) return (null, uri.Port, false);

        foreach (var app in workspace.Applications)
            foreach (var port in app.AllocatedPorts.Where(p => p.AllocatedPort == uri.Port))
                return (app.Name, port.AllocatedPort, port.HealthCheckPath is not null);

        return (null, uri.Port, false);
    }

    private void StartStandaloneProbe(string workspaceId, string url, CancellationToken serverStopped)
    {
        CancelStandaloneProbe(workspaceId);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(serverStopped);
        _standaloneProbes[workspaceId] = cts;
        _ = Task.Run(() => RunStandaloneProbeAsync(workspaceId, url, cts.Token), cts.Token);
    }

    private void CancelStandaloneProbe(string workspaceId)
    {
        if (_standaloneProbes.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task RunStandaloneProbeAsync(string workspaceId, string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            for (var attempt = 1; attempt <= StandaloneMaxAttempts && !ct.IsCancellationRequested; attempt++)
            {
                if (await ProbeAsync(url, ct))
                {
                    UpdatePortHealth(workspaceId, url, healthy: true);
                    break;
                }
                PublishConnectingAttempt(workspaceId, attempt);
                if (attempt < StandaloneMaxAttempts)
                {
                    if (!await Delay(StandaloneRetryIntervalMs, ct)) return;
                }
                else if (!ct.IsCancellationRequested) PublishFailed(workspaceId);
            }

            // Loop: keep probing to detect the app going down after connecting.
            while (!ct.IsCancellationRequested)
            {
                if (!await Delay(5000, ct)) return;
                if (!await ProbeAsync(url, ct))
                {
                    UpdatePortHealth(workspaceId, url, healthy: false);
                    break;
                }
            }
        }
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

    private void UpdatePortHealth(string workspaceId, string url, bool healthy)
    {
        // Standalone probes don't drive PortHealth (that map is only consulted for
        // health-checked targets, and standalone targets carry HealthChecked=false).
        // Reaching this point means the standalone probe transitioned to reachable —
        // the state derives from session-liveness from here. Publish a recomputation
        // so that transition surfaces immediately.
        _ = url;
        _ = healthy;
        RecomputeAndPublish(workspaceId);
    }

    private void PublishConnectingAttempt(string workspaceId, int attempt)
    {
        var state = StreamState.Connecting(attempt, StandaloneMaxAttempts);
        lock (_lock)
        {
            _lastPublished.TryGetValue(workspaceId, out var previous);
            if (previous is not null && previous == state) return;
            _lastPublished[workspaceId] = state;
        }
        _eventBus.PublishStreamState(workspaceId, state);
    }

    private void PublishFailed(string workspaceId)
    {
        var state = StreamState.Failed($"App unreachable after {StandaloneMaxAttempts} attempts.", StandaloneMaxAttempts);
        lock (_lock) _lastPublished[workspaceId] = state;
        _eventBus.PublishStreamState(workspaceId, state);
    }

    private static async Task<bool> Delay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    }

    private static string HealthKey(string appName, int port) => $"{appName}:{port}";
}
