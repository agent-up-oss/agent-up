using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Models;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceReadinessService(
    WorkspaceRegistry registry,
    ProcessesController processes,
    DesktopApplicationsController desktopApplications,
    IApplicationReadinessProbe probe,
    IApplicationReadinessClock clock,
    ApplicationReadinessOptions options,
    ILogger<WorkspaceReadinessService> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _workspaceCts = new();
    private readonly ConcurrentDictionary<string, Task> _pollers = new();
    private readonly ConcurrentDictionary<(string WorkspaceId, string AppName), DateTimeOffset> _startedAt = new();

    public async Task WatchAsync(string workspaceId)
    {
        var cts = RentCts(workspaceId);
        RecordStartingApps(workspaceId);
        await EvaluateAsync(workspaceId, cts.Token);
        if (cts.IsCancellationRequested || !HasStartingApps(workspaceId))
            return;

        StartPoller(workspaceId, cts.Token);
    }

    public void Stop(string workspaceId)
    {
        if (_workspaceCts.TryRemove(workspaceId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        _pollers.TryRemove(workspaceId, out _);
        foreach (var key in _startedAt.Keys.Where(key => key.WorkspaceId == workspaceId).ToList())
            _startedAt.TryRemove(key, out _);
    }

    public void Dispose()
    {
        foreach (var workspaceId in _workspaceCts.Keys.ToList())
            Stop(workspaceId);
    }

    private CancellationTokenSource RentCts(string workspaceId)
    {
        var cts = _workspaceCts.GetOrAdd(workspaceId, _ => new CancellationTokenSource());
        if (!cts.IsCancellationRequested)
            return cts;

        var replacement = new CancellationTokenSource();
        if (_workspaceCts.TryUpdate(workspaceId, replacement, cts))
        {
            cts.Dispose();
            return replacement;
        }

        replacement.Dispose();
        return _workspaceCts.GetValueOrDefault(workspaceId) ?? cts;
    }

    private void StartPoller(string workspaceId, CancellationToken token)
    {
        _pollers.AddOrUpdate(
            workspaceId,
            _ => RunPoller(workspaceId, token),
            (_, existing) => existing.IsCompleted ? RunPoller(workspaceId, token) : existing);
    }

    private Task RunPoller(string workspaceId, CancellationToken token)
    {
        Task poller = null!;
        poller = Task.Run(() => PollAsync(workspaceId, token, () => poller), token);
        return poller;
    }

    private async Task PollAsync(string workspaceId, CancellationToken cancellationToken, Func<Task> self)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && HasStartingApps(workspaceId))
            {
                try
                {
                    await clock.Delay(options.PollInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await EvaluateAsync(workspaceId, cancellationToken);
            }
        }
        finally
        {
            _pollers.TryRemove(new KeyValuePair<string, Task>(workspaceId, self()));
        }
    }

    private async Task EvaluateAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = registry.GetById(workspaceId);
        if (workspace is null)
            return;

        foreach (var app in workspace.Applications.Where(application => application.State == ApplicationState.Starting).ToList())
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var startedAt = _startedAt.GetOrAdd((workspaceId, app.Name), _ => clock.UtcNow);
            if (clock.UtcNow - startedAt >= options.Deadline)
            {
                var message = ApplicationReadinessSignal.For(app).TimeoutMessage(options.Deadline);
                await FailApplicationAsync(workspaceId, app.Name, message);
                continue;
            }

            if (!processes.IsApplicationAlive(workspaceId, app.Name))
                continue;

            if (await IsReadyAsync(app, startedAt, cancellationToken))
            {
                await registry.UpdateApplicationStateAsync(workspaceId, app.Name, ApplicationState.Running);
                ForgetStart(workspaceId, app.Name);
            }
        }

        await ReconcileWorkspaceAsync(workspaceId);
    }

    private async Task<bool> IsReadyAsync(
        ApplicationInstance app,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var signal = ApplicationReadinessSignal.For(app);
        var tcpReady = false;
        var httpReady = false;
        if (signal.Kind == ApplicationReadinessKind.TcpPort && signal.Port is int tcpPort)
            tcpReady = await probe.TcpReadyAsync(tcpPort, cancellationToken);
        if (signal.Kind == ApplicationReadinessKind.HealthCheck && signal.Port is int httpPort && signal.Path is not null)
            httpReady = await probe.HttpReadyAsync(httpPort, signal.Path, cancellationToken);

        return ApplicationReadiness.IsReady(
            signal,
            processAlive: true,
            tcpReady,
            httpReady,
            ApplicationReadiness.HasSettled(startedAt, clock.UtcNow, options.SettleWindow));
    }

    private async Task ReconcileWorkspaceAsync(string workspaceId)
    {
        var workspace = registry.GetById(workspaceId);
        if (workspace is null || workspace.State != WorkspaceState.Starting)
            return;

        var next = WorkspaceStartEvaluation.StateFrom(workspace.Applications);
        if (next == WorkspaceState.Starting)
            return;

        var failures = WorkspaceStartEvaluation.Failures(workspace);
        if (next == WorkspaceState.Failed)
        {
            await processes.KillWorkspaceAsync(workspaceId);
            await desktopApplications.StopWorkspaceAsync(workspaceId, CancellationToken.None);
        }

        await registry.UpdateLastErrorAsync(workspaceId, next == WorkspaceState.Failed || next == WorkspaceState.Degraded
            ? WorkspaceStartEvaluation.Summarize(failures)
            : null);
        await registry.UpdateStateAsync(workspaceId, next);
        logger.LogInformation("Workspace {WorkspaceId} left Starting for {State}", workspaceId, next);
    }

    private async Task FailApplicationAsync(string workspaceId, string appName, string message)
    {
        await registry.UpdateApplicationStateAsync(workspaceId, appName, ApplicationState.Failed, message);
        ForgetStart(workspaceId, appName);
        logger.LogWarning("Application {Application} in workspace {WorkspaceId} failed readiness: {Message}",
            appName, workspaceId, message);
    }

    private void ForgetStart(string workspaceId, string appName)
        => _startedAt.TryRemove((workspaceId, appName), out _);

    private void RecordStartingApps(string workspaceId)
    {
        var workspace = registry.GetById(workspaceId);
        if (workspace is null)
            return;

        var now = clock.UtcNow;
        foreach (var app in workspace.Applications.Where(application => application.State == ApplicationState.Starting))
            _startedAt.TryAdd((workspaceId, app.Name), now);
    }

    private bool HasStartingApps(string workspaceId)
    {
        var workspace = registry.GetById(workspaceId);
        return workspace is not null
            && workspace.Applications.Any(application => application.State == ApplicationState.Starting);
    }
}
