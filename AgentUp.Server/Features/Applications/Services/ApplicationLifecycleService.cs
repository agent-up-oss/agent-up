using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Applications.Services;

public sealed class ApplicationLifecycleService
{
    private readonly WorkspaceRegistry _registry;
    private readonly ProcessesController _processes;

    public ApplicationLifecycleService(WorkspaceRegistry registry, ProcessesController processes)
    {
        _registry = registry;
        _processes = processes;
    }

    public IReadOnlyList<ApplicationInstance>? GetApplications(string workspaceId)
        => _registry.GetById(workspaceId)?.Applications;

    public async Task<ApplicationLifecycleResult> StartAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Starting);
        try
        {
            await _processes.LaunchApplicationAsync(workspace, applicationName);
            await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Running);
            return ApplicationLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Failed);
            return ApplicationLifecycleResult.Failed(ex.Message);
        }
    }

    public async Task<ApplicationLifecycleResult> StopAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Stopping);
        await _processes.KillApplicationAsync(workspaceId, applicationName);
        await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Stopped);
        return ApplicationLifecycleResult.Success();
    }

    public async Task<ApplicationLifecycleResult> RestartAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Starting);
        try
        {
            await _processes.KillApplicationAsync(workspaceId, applicationName);
            await _processes.LaunchApplicationAsync(workspace, applicationName);
            await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Running);
            return ApplicationLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Failed);
            return ApplicationLifecycleResult.Failed(ex.Message);
        }
    }

    public async Task<ApplicationOutputResult> GetOutputAsync(string workspaceId, string applicationName)
    {
        if (Resolve(workspaceId, applicationName) is null)
            return ApplicationOutputResult.NotFound();

        return ApplicationOutputResult.Success(await _processes.GetOutputAsync(workspaceId, applicationName));
    }

    private Workspace? Resolve(string workspaceId, string applicationName)
    {
        var workspace = _registry.GetById(workspaceId);
        return workspace is null || workspace.Applications.All(app => app.Name != applicationName)
            ? null
            : workspace;
    }
}

public sealed record ApplicationLifecycleResult(bool Found, bool Succeeded, string? Error)
{
    public static ApplicationLifecycleResult NotFound() => new(false, false, null);

    public static ApplicationLifecycleResult Success() => new(true, true, null);

    public static ApplicationLifecycleResult Failed(string error) => new(true, false, error);
}

public sealed record ApplicationOutputResult(bool Found, IReadOnlyList<string> Lines)
{
    public static ApplicationOutputResult NotFound() => new(false, []);

    public static ApplicationOutputResult Success(IReadOnlyList<string> lines) => new(true, lines);
}
