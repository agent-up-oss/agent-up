using System.Diagnostics;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceLifecycleService
{
    private readonly WorkspaceRegistry _registry;
    private readonly ProcessesController _processes;

    public WorkspaceLifecycleService(WorkspaceRegistry registry, ProcessesController processes)
    {
        _registry = registry;
        _processes = processes;
    }

    public async Task<WorkspaceLifecycleResult> StartAsync(string id)
    {
        var workspace = _registry.GetById(id);
        if (workspace is null)
            return WorkspaceLifecycleResult.NotFound();

        await _registry.UpdateStateAsync(id, WorkspaceState.Starting);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);

        try
        {
            await _registry.ReallocatePortsAsync(id);
            await _processes.LaunchWorkspaceAsync(workspace);
            await _registry.UpdateStateAsync(id, WorkspaceState.Running);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);

            return WorkspaceLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
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
            await _processes.KillWorkspaceAsync(id);
            await _registry.UpdateStateAsync(id, WorkspaceState.Stopped);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Stopped);

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
