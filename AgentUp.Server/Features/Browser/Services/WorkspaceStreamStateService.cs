using System.Collections.Concurrent;
using AgentUp.Browser.Streaming;
using AgentUp.Browser.Streaming.Models;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

// Single source of truth for what state the workspace's AI viewer should show.
// All prior signals (chromium download, browser-connectivity retries, session
// liveness) collapse into one derived StreamState per workspace, published as a
// single SSE event kind. Cache is cleared on workspace stop/remove — no stale
// "connected" surviving a lifecycle transition.
public sealed class WorkspaceStreamStateService : IDisposable, IStreamSessionEventSink
{
    private const int StandaloneRetryIntervalMs = 2000;

    private readonly BrowserEventBus _eventBus;
    private readonly AppHealthController _healthChecks;
    private readonly WorkspaceQueryController _workspaceQuery;
    private readonly AuditController _audit;
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
        AuditController audit,
        ILogger<WorkspaceStreamStateService> logger)
    {
        _eventBus = eventBus;
        _healthChecks = healthChecks;
        _workspaceQuery = workspaceQuery;
        _audit = audit;
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
                StandaloneProbeAttempt = 0,
                StandaloneProbeReachable = false,
                StandaloneProbeExhausted = false,
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
                    StandaloneProbeAttempt = 0,
                    StandaloneProbeReachable = false,
                    StandaloneProbeExhausted = false,
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

    // ── Browser session signals (IStreamSessionEventSink) ────────────

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

    // ── Navigation target (IStreamSessionEventSink) ──────────────────

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
                // Reset probe state for the new target — otherwise Compute would return
                // Streaming immediately if the previous target was Reachable, showing a
                // WebView while the new URL hasn't been verified yet.
                StandaloneProbeAttempt = 0,
                StandaloneProbeReachable = false,
                StandaloneProbeExhausted = false,
            };
        }

        if (!healthChecked) StartStandaloneProbe(workspaceId, url, serverStopped);
        else CancelStandaloneProbe(workspaceId);

        RecomputeAndPublish(workspaceId);
    }

    // ── AppHealthController → per-port health ─────────────────────────

    private void HandlePortHealthChanged(string workspaceId, string appName, int port, bool isHealthy)
    {
        var key = StreamStateDerivation.HealthKey(appName, port);
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
        WorkspaceStreamInputs? snapshot;
        lock (_lock)
        {
            _inputs.TryGetValue(workspaceId, out snapshot);
            // Removed workspaces publish one final WorkspaceStopped so any live UI clears.
            var (chromiumState, chromiumProgress) = _chromium;
            next = snapshot is not null
                ? StreamStateDerivation.Compute(snapshot, chromiumState, chromiumProgress)
                : StreamState.Stopped();
            _lastPublished.TryGetValue(workspaceId, out previous);
            _lastPublished[workspaceId] = next;
        }

        if (previous is not null && previous == next) return;
        _eventBus.PublishStreamState(workspaceId, next);
        AuditTransition(workspaceId, previous, next, snapshot);
    }

    // Persist every server-side state transition to the audit log. Paired with the
    // desktop's `stream_state_changed` audit events, this gives a bidirectional trace:
    // the server row shows what Compute() derived and from which inputs; the desktop
    // row shows what the SSE consumer actually received and rendered.
    private void AuditTransition(string workspaceId, StreamState? previous, StreamState next, WorkspaceStreamInputs? snapshot)
    {
        var (chromiumState, chromiumProgress) = _chromium;
        var details = new Dictionary<string, string>
        {
            ["fromKind"] = previous?.Kind.ToString() ?? "<initial>",
            ["toKind"] = next.Kind.ToString(),
            ["chromiumState"] = chromiumState,
            ["chromiumProgress"] = chromiumProgress.ToString(),
            ["isRunning"] = (snapshot?.IsRunning ?? false).ToString(),
            ["sessionActive"] = (snapshot?.SessionActive ?? false).ToString(),
            ["currentTargetUrl"] = snapshot?.CurrentTarget?.Url ?? string.Empty,
            ["currentTargetPort"] = (snapshot?.CurrentTarget?.Port ?? 0).ToString(),
            ["currentTargetHealthChecked"] = (snapshot?.CurrentTarget?.HealthChecked ?? false).ToString(),
            ["portHealth"] = snapshot is null
                ? string.Empty
                : string.Join(",", snapshot.PortHealth.Select(kv => $"{kv.Key}={kv.Value}")),
            ["probeAttempt"] = (snapshot?.StandaloneProbeAttempt ?? 0).ToString(),
            ["probeReachable"] = (snapshot?.StandaloneProbeReachable ?? false).ToString(),
            ["probeExhausted"] = (snapshot?.StandaloneProbeExhausted ?? false).ToString(),
        };
        _ = _audit.RecordAsync(
            new AuditRecordRequest(
                Kind: "stream",
                Source: "server",
                Action: "state_transition",
                Outcome: next.Kind.ToString(),
                WorkspaceId: workspaceId,
                Details: details),
            CancellationToken.None);
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
            for (var attempt = 1; attempt <= StreamStateDerivation.StandaloneMaxAttempts && !ct.IsCancellationRequested; attempt++)
            {
                if (await ProbeAsync(url, ct))
                {
                    UpdateProbeState(workspaceId, attempt, reachable: true, exhausted: false);
                    goto steadyState;
                }
                UpdateProbeState(workspaceId, attempt, reachable: false, exhausted: false);
                if (attempt < StreamStateDerivation.StandaloneMaxAttempts)
                {
                    if (!await Delay(StandaloneRetryIntervalMs, ct)) return;
                }
                else if (!ct.IsCancellationRequested)
                {
                    UpdateProbeState(workspaceId, attempt, reachable: false, exhausted: true);
                }
            }

            steadyState:
            while (!ct.IsCancellationRequested)
            {
                if (!await Delay(5000, ct)) return;
                if (!await ProbeAsync(url, ct))
                {
                    UpdateProbeState(workspaceId, attempt: 1, reachable: false, exhausted: false);
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

    // Sole writer of probe fields. All probe-driven state transitions flow through
    // RecomputeAndPublish so Compute is authoritative — after a workspace stop,
    // IsRunning=false wins over any in-flight probe observation.
    internal void UpdateProbeState(string workspaceId, int attempt, bool reachable, bool exhausted)
    {
        lock (_lock)
        {
            if (!_inputs.TryGetValue(workspaceId, out var current)) return;
            _inputs[workspaceId] = current with
            {
                StandaloneProbeAttempt = attempt,
                StandaloneProbeReachable = reachable,
                StandaloneProbeExhausted = exhausted,
            };
        }
        RecomputeAndPublish(workspaceId);
    }

    private static async Task<bool> Delay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    }
}
