using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;

namespace AgentUp.Server.Features.Applications.Services;

public sealed class ApplicationLifecycleService
{
    private readonly WorkspaceQueryController _workspaces;
    private readonly WorkspaceStateController _states;
    private readonly ProcessesController _processes;
    private readonly DesktopApplicationsController _desktopApplications;
    private readonly WorkspaceReadinessController _readiness;

    public ApplicationLifecycleService(
        WorkspaceQueryController workspaces,
        WorkspaceStateController states,
        ProcessesController processes,
        DesktopApplicationsController desktopApplications,
        WorkspaceReadinessController readiness)
    {
        _workspaces = workspaces;
        _states = states;
        _processes = processes;
        _desktopApplications = desktopApplications;
        _readiness = readiness;
    }

    public IReadOnlyList<ApplicationInstance>? GetApplications(string workspaceId)
        => _workspaces.GetById(workspaceId)?.Applications;

    public async Task<ApplicationLifecycleResult> StartAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Starting);
        try
        {
            var application = workspace.Applications.Single(app => app.Name == applicationName);
            if (application.Kind == ApplicationKind.Desktop)
                application.RuntimeEnvironment = await _desktopApplications.PrepareAsync(workspace, application, CancellationToken.None);
            await _processes.LaunchApplicationAsync(workspace, applicationName);
            if (IsFailed(workspaceId, applicationName))
                return ApplicationLifecycleResult.Failed("Application could not be started.");
            await _readiness.WatchAsync(workspaceId);
            if (IsFailed(workspaceId, applicationName))
                return ApplicationLifecycleResult.Failed("Application could not be started.");
            return ApplicationLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await _desktopApplications.StopAsync(workspaceId, applicationName, CancellationToken.None);
            await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Failed);
            return ApplicationLifecycleResult.Failed("Application could not be started.");
        }
    }

    public async Task<ApplicationLifecycleResult> StopAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Stopping);
        try
        {
            await _processes.KillApplicationAsync(workspaceId, applicationName);
            await _desktopApplications.StopAsync(workspaceId, applicationName, CancellationToken.None);
            await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Stopped);
            return ApplicationLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await _desktopApplications.StopAsync(workspaceId, applicationName, CancellationToken.None);
            await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Failed);
            return ApplicationLifecycleResult.Failed("Application could not be stopped.");
        }
    }

    public async Task<ApplicationLifecycleResult> RestartAsync(string workspaceId, string applicationName)
    {
        var workspace = Resolve(workspaceId, applicationName);
        if (workspace is null)
            return ApplicationLifecycleResult.NotFound();

        await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Starting);
        try
        {
            await _processes.KillApplicationAsync(workspaceId, applicationName);
            var application = workspace.Applications.Single(app => app.Name == applicationName);
            if (application.Kind == ApplicationKind.Desktop)
                application.RuntimeEnvironment = await _desktopApplications.PrepareAsync(workspace, application, CancellationToken.None);
            await _processes.LaunchApplicationAsync(workspace, applicationName);
            if (IsFailed(workspaceId, applicationName))
                return ApplicationLifecycleResult.Failed("Application could not be restarted.");
            await _readiness.WatchAsync(workspaceId);
            if (IsFailed(workspaceId, applicationName))
                return ApplicationLifecycleResult.Failed("Application could not be restarted.");
            return ApplicationLifecycleResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await _desktopApplications.StopAsync(workspaceId, applicationName, CancellationToken.None);
            await _states.UpdateApplicationStateAsync(workspaceId, applicationName, ApplicationState.Failed);
            return ApplicationLifecycleResult.Failed("Application could not be restarted.");
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
        var workspace = _workspaces.GetById(workspaceId);
        return workspace is null || workspace.Applications.All(app => app.Name != applicationName)
            ? null
            : workspace;
    }

    private bool IsFailed(string workspaceId, string applicationName)
        => _workspaces.GetById(workspaceId)?.Applications
            .FirstOrDefault(app => app.Name == applicationName)?.State == ApplicationState.Failed;
}

public sealed record ApplicationLifecycleResult(bool Found, bool Succeeded, string? Error)
{
    public static ApplicationLifecycleResult NotFound() => new(false, false, null);

    public static ApplicationLifecycleResult Success() => new(true, true, null);

    public static ApplicationLifecycleResult Failed(string error) => new(true, false, error);
}

public sealed record ApplicationOutputResult(bool Found, IReadOnlyList<ProcessOutputLineDto> Lines)
{
    public static ApplicationOutputResult NotFound() => new(false, []);

    public static ApplicationOutputResult Success(IReadOnlyList<ProcessOutputLineDto> lines) => new(true, lines);
}
