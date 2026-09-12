using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceLifecycleService
{
    private readonly WorkspaceRegistry _registry;
    private readonly ProcessesController _processes;
    private readonly BrowserLifecycleController _browser;
    private readonly DesktopApplicationsController _desktopApplications;
    private readonly AppHealthController _healthChecks;
    private readonly AppMetricsController _metricsPulls;
    private readonly WorkspaceStreamStateController _streamState;
    private readonly OrchestrationRegistrationController _registration;
    private readonly ILogger<WorkspaceLifecycleService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _transitionLocks = new();

    public WorkspaceLifecycleService(
        WorkspaceRegistry registry,
        ProcessesController processes,
        BrowserLifecycleController browser,
        DesktopApplicationsController desktopApplications,
        AppHealthController healthChecks,
        AppMetricsController metricsPulls,
        WorkspaceStreamStateController streamState,
        OrchestrationRegistrationController registration,
        ILogger<WorkspaceLifecycleService> logger)
    {
        _registry = registry;
        _processes = processes;
        _browser = browser;
        _desktopApplications = desktopApplications;
        _healthChecks = healthChecks;
        _metricsPulls = metricsPulls;
        _streamState = streamState;
        _registration = registration;
        _logger = logger;
    }

    public async Task<WorkspaceLifecycleResult> StartAsync(string id)
    {
        var gate = GetTransitionLock(id);
        await gate.WaitAsync();
        try
        {
            var workspace = _registry.GetById(id);
            if (workspace is null)
                return WorkspaceLifecycleResult.NotFound();

            if (workspace.State is WorkspaceState.Running or WorkspaceState.Starting)
            {
                gate.Release();
                WorkspaceLifecycleResult stopResult;
                try
                {
                    stopResult = await StopAsync(id);
                }
                finally
                {
                    await gate.WaitAsync();
                }

                if (!stopResult.Found)
                    return WorkspaceLifecycleResult.NotFound();
                if (!stopResult.Succeeded)
                    return stopResult;

                workspace = _registry.GetById(id);
                if (workspace is null)
                    return WorkspaceLifecycleResult.NotFound();
            }

            await TryRefreshWorkspaceDefinitionAsync(workspace);

            workspace = _registry.GetById(id)!;

            await _registry.UpdateStateAsync(id, WorkspaceState.Starting);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);

            // Dispose any stale browser session from a previous run so the first navigate
            // after this start creates a fresh Chromium session at the correct URL.
            await _browser.DisposeSessionAsync(id);

            try
            {
                await _registry.ReallocatePortsAsync(id);
                workspace = _registry.GetById(id)!;
                foreach (var app in workspace.Applications.Where(app => app.Kind == ApplicationKind.Desktop))
                    app.RuntimeEnvironment = await _desktopApplications.PrepareAsync(workspace, app, CancellationToken.None);
                await _processes.LaunchWorkspaceAsync(workspace);
                await _registry.UpdateStateAsync(id, WorkspaceState.Running);
                await _registry.UpdateLastErrorAsync(id, null);
                foreach (var app in workspace.Applications)
                    await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);

                _streamState.OnWorkspaceStarted(workspace);
                _healthChecks.StartForWorkspace(workspace);
                _metricsPulls.StartForWorkspace(workspace);

                return WorkspaceLifecycleResult.Success();
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Workspace failed to start");
                await _desktopApplications.StopWorkspaceAsync(id, CancellationToken.None);
                await _registry.UpdateLastErrorAsync(id, ex.Message);
                await _registry.UpdateStateAsync(id, WorkspaceState.Failed);
                return WorkspaceLifecycleResult.Failed("Workspace could not be started.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WorkspaceLifecycleResult> StopAsync(string id)
    {
        var gate = GetTransitionLock(id);
        await gate.WaitAsync();
        try
        {
            var workspace = _registry.GetById(id);
            if (workspace is null)
                return WorkspaceLifecycleResult.NotFound();

            // Idempotent: a second Stop on an already-Stopped workspace is a no-op.
            if (workspace.State is WorkspaceState.Stopped or WorkspaceState.Stopping)
                return WorkspaceLifecycleResult.Success();

            await _registry.UpdateStateAsync(id, WorkspaceState.Stopping);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopping);

            try
            {
                // Publish WorkspaceStopped early so any live desktop clients hide the WebView
                // and show the correct banner before we start tearing down the session.
                _streamState.OnWorkspaceStopped(id);
                _healthChecks.StopForWorkspace(id);
                _metricsPulls.StopForWorkspace(id);
                await _processes.KillWorkspaceAsync(id);
                await _desktopApplications.StopWorkspaceAsync(id, CancellationToken.None);
                await _registry.UpdateStateAsync(id, WorkspaceState.Stopped);
                foreach (var app in workspace.Applications)
                    await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopped);

                // Dispose the headless browser session so the display loop stops streaming stale
                // content, and disconnect viewer WebSockets so clients reconnect after restart.
                await _browser.DisposeSessionAsync(id);
                await _browser.DisconnectAllAsync(id, CancellationToken.None);

                return WorkspaceLifecycleResult.Success();
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Workspace failed to stop");
                await _registry.UpdateStateAsync(id, WorkspaceState.Failed);
                return WorkspaceLifecycleResult.Failed("Workspace could not be stopped.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    // Start and Stop both read-then-write a workspace's state across several awaits
    // (registry updates, process launch/kill, health and metrics polling). Without this,
    // a concurrent Start/Stop pair for the same workspace can interleave — e.g. Stop
    // finishing while Start is still awaiting LaunchWorkspaceAsync, which would then mark
    // the workspace Running and resume polling after the Stop already tore it down.
    private SemaphoreSlim GetTransitionLock(string workspaceId)
        => _transitionLocks.GetOrAdd(workspaceId, static _ => new SemaphoreSlim(1, 1));

    public async Task<int> CleanupTutorialWorkspacesAsync()
    {
        var workspaces = _registry.GetAll().ToList();

        foreach (var workspace in workspaces)
        {
            _metricsPulls.StopForWorkspace(workspace.Id);

            try
            {
                await _processes.KillWorkspaceAsync(workspace.Id);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning(ex.Message);
            }

            await _registry.RemoveAsync(workspace.Id);
        }

        return workspaces.Count;
    }

    private async Task TryRefreshWorkspaceDefinitionAsync(Workspace workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace.WorktreePath))
            return;

        try
        {
            var request = await _registration.BuildAsync(workspace.WorktreePath, CancellationToken.None);
            if (request is null)
                return;

            await _registry.RegisterAsync(request);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                     or FileNotFoundException or DirectoryNotFoundException
                                     or JsonException)
        {
            // workspace.Id echoes the caller-supplied route id; CodeQL's log-forging query flags
            // it regardless of validation, so it is kept out of this log line.
            _logger.LogWarning(ex, "Could not refresh agent-up.json for workspace");
        }
    }
}
