using System.Diagnostics;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceLifecycleService
{
    private readonly WorkspaceRegistry _registry;
    private readonly ProcessesController _processes;
    private readonly BrowserLifecycleController _browser;
    private readonly AppHealthController _healthChecks;
    private readonly WorkspaceStreamStateController _streamState;
    private readonly ILogger<WorkspaceLifecycleService> _logger;

    public WorkspaceLifecycleService(
        WorkspaceRegistry registry,
        ProcessesController processes,
        BrowserLifecycleController browser,
        AppHealthController healthChecks,
        WorkspaceStreamStateController streamState,
        ILogger<WorkspaceLifecycleService> logger)
    {
        _registry = registry;
        _processes = processes;
        _browser = browser;
        _healthChecks = healthChecks;
        _streamState = streamState;
        _logger = logger;
    }

    public async Task<WorkspaceLifecycleResult> StartAsync(string id)
    {
        var workspace = _registry.GetById(id);
        if (workspace is null)
            return WorkspaceLifecycleResult.NotFound();

        await _registry.UpdateStateAsync(id, WorkspaceState.Starting);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);

        // Dispose any stale browser session from a previous run so the first navigate
        // after this start creates a fresh Chromium session at the correct URL.
        await _browser.DisposeSessionAsync(id);

        try
        {
            await _registry.ReallocatePortsAsync(id);
            await _processes.LaunchWorkspaceAsync(workspace);
            await _registry.UpdateStateAsync(id, WorkspaceState.Running);
            await _registry.UpdateLastErrorAsync(id, null);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);

            _streamState.OnWorkspaceStarted(workspace);
            _healthChecks.StartForWorkspace(workspace);

            return WorkspaceLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Workspace failed to start");
            await _registry.UpdateLastErrorAsync(id, ex.Message);
            await _registry.UpdateStateAsync(id, WorkspaceState.Failed);
            return WorkspaceLifecycleResult.Failed("Workspace could not be started.");
        }
    }

    public async Task<WorkspaceLifecycleResult> StopAsync(string id)
    {
        var workspace = _registry.GetById(id);
        if (workspace is null)
            return WorkspaceLifecycleResult.NotFound();

        await _registry.UpdateStateAsync(id, WorkspaceState.Stopping);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopping);

        try
        {
            // Publish WorkspaceStopped early so any live desktop clients hide the WebView
            // and show the correct banner before we start tearing down the session.
            _streamState.OnWorkspaceStopped(id);
            _healthChecks.StopForWorkspace(id);
            await _processes.KillWorkspaceAsync(id);
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
            await _registry.UpdateStateAsync(id, WorkspaceState.Failed);
            return WorkspaceLifecycleResult.Failed("Workspace could not be stopped.");
        }
    }

    public async Task<int> CleanupTutorialWorkspacesAsync()
    {
        var workspaces = _registry.GetAll().ToList();

        foreach (var workspace in workspaces)
        {
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
}

public sealed record WorkspaceLifecycleResult(bool Found, bool Succeeded, string? Error)
{
    public static WorkspaceLifecycleResult NotFound() => new(false, false, null);

    public static WorkspaceLifecycleResult Success() => new(true, true, null);

    public static WorkspaceLifecycleResult Failed(string error) => new(true, false, error);
}
