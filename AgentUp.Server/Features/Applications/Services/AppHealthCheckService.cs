using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Applications.Services;

public sealed class AppHealthCheckService(
    WorkspaceQueryController workspaceQuery,
    WorkspaceStateController workspaceState,
    AuditController audit,
    ILogger<AppHealthCheckService> logger) : IDisposable
{
    private readonly ConcurrentDictionary<(string WorkspaceId, string AppName, int Port), string> _portHealthState = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _workspaceCts = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };

    // (workspaceId, appName, allocatedPort, isHealthy)
    public event Action<string, string, int, bool>? PortHealthChanged;

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
            foreach (var port in app.AllocatedPorts.Where(p => p.HealthCheckPath is not null))
            {
                _portHealthState[(workspace.Id, app.Name, port.AllocatedPort)] = "Checking";
                workspaceState.SetApplicationHealthState(workspace.Id, app.Name, AggregateAppState(workspace.Id, app.Name));
                _ = Task.Run(
                    () => RunPortProbeAsync(workspace.Id, app.Name, port.AllocatedPort, port.HealthCheckPath!, cts.Token),
                    cts.Token);
            }
        }

        if (HasAnyHealthChecks(workspace))
            PublishEnrichedEvent(workspace.Id);
    }

    public void StopForWorkspace(string workspaceId)
    {
        if (_workspaceCts.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        foreach (var key in _portHealthState.Keys.Where(k => k.WorkspaceId == workspaceId).ToList())
            _portHealthState.TryRemove(key, out _);
    }

    public IReadOnlyList<PortHealthChange>? GetPortHealth(string workspaceId, string appName)
    {
        var entries = _portHealthState
            .Where(kv => kv.Key.WorkspaceId == workspaceId && kv.Key.AppName == appName)
            .Select(kv => new PortHealthChange(kv.Key.Port, kv.Value))
            .ToList();
        return entries.Count > 0 ? entries : null;
    }

    public string? GetWorkspaceHealth(string workspaceId)
    {
        var states = _portHealthState
            .Where(kv => kv.Key.WorkspaceId == workspaceId)
            .Select(kv => kv.Value)
            .ToList();
        return states.Count > 0 ? AggregateStates(states) : null;
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

    private async Task RunPortProbeAsync(
        string workspaceId, string appName, int allocatedPort, string path, CancellationToken ct)
    {
        var url = $"http://localhost:{allocatedPort}{path}";
        bool? lastHealthy = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            bool isHealthy;
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                isHealthy = (int)response.StatusCode < 500;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                isHealthy = false;
            }

            if (isHealthy == lastHealthy) continue;

            var newState = isHealthy ? "Healthy" : "Unhealthy";
            _portHealthState[(workspaceId, appName, allocatedPort)] = newState;

            workspaceState.SetApplicationHealthState(workspaceId, appName, AggregateAppState(workspaceId, appName));

            logger.LogDebug("Port health changed: {WorkspaceId}/{AppName}:{Port} → {State}",
                workspaceId, appName, allocatedPort, newState);

            PublishEnrichedEvent(workspaceId);

            _ = audit.RecordAsync(new AuditRecordRequest(
                Kind: "health",
                Source: "server",
                Action: "port_health_check",
                Outcome: isHealthy ? "healthy" : "unhealthy",
                WorkspaceId: workspaceId,
                Details: new Dictionary<string, string>
                {
                    ["appName"] = appName,
                    ["port"] = allocatedPort.ToString(),
                    ["url"] = url,
                    ["state"] = newState
                }), CancellationToken.None);

            PortHealthChanged?.Invoke(workspaceId, appName, allocatedPort, isHealthy);
            lastHealthy = isHealthy;
        }
    }

    private ApplicationState AggregateAppState(string workspaceId, string appName)
    {
        var portStates = _portHealthState
            .Where(kv => kv.Key.WorkspaceId == workspaceId && kv.Key.AppName == appName)
            .Select(kv => kv.Value)
            .ToList();

        if (portStates.Count == 0) return ApplicationState.Running;
        if (portStates.Any(s => s == "Unhealthy")) return ApplicationState.Unhealthy;
        if (portStates.Any(s => s == "Checking")) return ApplicationState.Checking;
        return ApplicationState.Running;
    }

    private static string AggregateStates(IReadOnlyList<string> states)
    {
        if (states.Any(s => s == "Unhealthy")) return "Unhealthy";
        if (states.Any(s => s == "Checking")) return "Checking";
        return "Healthy";
    }

    private static bool HasAnyHealthChecks(Workspace workspace)
        => workspace.Applications.Any(a => a.AllocatedPorts.Any(p => p.HealthCheckPath is not null));

    private void PublishEnrichedEvent(string workspaceId)
    {
        var workspace = workspaceQuery.GetById(workspaceId);
        if (workspace is null) return;

        var appChanges = workspace.Applications
            .Select(a => new AppStateChange(a.Name, a.State.ToString(), GetPortHealth(workspaceId, a.Name)))
            .ToList();

        workspaceState.PublishEnrichedEvent(workspaceId, appChanges, GetWorkspaceHealth(workspaceId));
    }
}
