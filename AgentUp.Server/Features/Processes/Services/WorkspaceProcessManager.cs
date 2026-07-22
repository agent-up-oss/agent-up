using System.Collections.Concurrent;
using System.Diagnostics;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Processes.Services;

public sealed partial class WorkspaceProcessManager : IWorkspaceProcessManager, IHostedService
{
    // key: (workspaceId, appName)
    private readonly ConcurrentDictionary<(string, string), Process> _processes = new();
    private readonly ConcurrentDictionary<(string, string), string> _containerNames = new();

    private readonly WorkspaceStateController _registry;
    private readonly IOutputRepository _output;
    private readonly ILocalProcessProvider _localProcesses;
    private readonly IDockerProcessProvider _docker;
    private readonly ILogger<WorkspaceProcessManager> _logger;

    public WorkspaceProcessManager(
        WorkspaceStateController registry,
        IOutputRepository output,
        ILocalProcessProvider localProcesses,
        IDockerProcessProvider docker,
        ILogger<WorkspaceProcessManager> logger)
    {
        _registry = registry;
        _output = output;
        _localProcesses = localProcesses;
        _docker = docker;
        _logger = logger;
    }

    public async Task LaunchAsync(Workspace workspace)
    {
        await KillAsync(workspace.Id);
        foreach (var app in workspace.Applications)
            await LaunchApplicationAsync(workspace, app.Name);
    }

    public async Task LaunchApplicationAsync(Workspace workspace, string appName)
    {
        var app = workspace.Applications.FirstOrDefault(a => a.Name == appName)
            ?? throw new InvalidOperationException($"Application '{appName}' not found in workspace.");

        await KillApplicationAsync(workspace.Id, appName);
        await _output.ClearAsync(workspace.Id, appName);

        if (app.CapabilityStatus is { CanRun: false })
        {
            foreach (var message in app.CapabilityStatus.Messages)
                await _output.AppendAsync(workspace.Id, appName, "[err] " + message);

            await _registry.UpdateApplicationStateAsync(workspace.Id, appName, ApplicationState.Failed);
            throw new InvalidOperationException($"Capability '{app.CapabilityStatus.CapabilityId}' cannot run '{appName}'.");
        }

        if (app.ServiceType == ServiceType.Docker)
        {
            await LaunchDockerServiceAsync(workspace, app);
            return;
        }

        var process = _localProcesses.CreateApplicationProcess(workspace, app);
        var workspaceId = workspace.Id;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _ = _output.AppendAsync(workspaceId, appName, e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _ = _output.AppendAsync(workspaceId, appName, "[err] " + e.Data);
        };

        process.Exited += (sender, args) =>
        {
            _processes.TryRemove((workspaceId, appName), out var exited);
            var exitCode = (sender as Process)?.ExitCode ?? -1;
            var exitState = exitCode == 0 ? ApplicationState.Stopped : ApplicationState.Failed;
            _ = _registry.UpdateApplicationStateAsync(workspaceId, appName, exitState);
            _logger.LogInformation("Workspace application process exited with code {Code}", exitCode);
            exited?.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[(workspaceId, appName)] = process;
        _logger.LogInformation("Started workspace application process with pid {Pid}", process.Id);
    }

    private async Task LaunchDockerServiceAsync(Workspace workspace, ApplicationInstance app)
    {
        var workspaceId = workspace.Id;
        var containerName = _docker.GetContainerName(workspaceId, app.Name);
        _containerNames[(workspaceId, app.Name)] = containerName;

        try
        {
            // Remove any stale container with this name
            await _docker.RunAsync("rm", "-f", containerName);

            var run = await _docker.RunAsync([.. _docker.CreateRunArguments(containerName, app, workspace.WorktreePath)]);
            if (run.ExitCode != 0)
            {
                await AppendDockerErrorAsync(workspaceId, app.Name, run.Stderr);
                throw new InvalidOperationException($"docker run failed for '{app.Name}': {run.Stderr.Trim()}");
            }

            _logger.LogInformation("Started Docker container for workspace application");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _containerNames.TryRemove((workspaceId, app.Name), out _);
            await _output.AppendAsync(workspaceId, app.Name, "[err] " + ex.Message);
            throw new InvalidOperationException($"docker failed for '{app.Name}': {ex.Message}", ex);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is System.ComponentModel.Win32Exception)
        {
            _containerNames.TryRemove((workspaceId, app.Name), out _);
            await _output.AppendAsync(workspaceId, app.Name, "[err] " + ex.Message);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _containerNames.TryRemove((workspaceId, app.Name), out _);
            throw;
        }

        // Tail logs for output capture
        var appName = app.Name;
        var logProcess = _docker.CreateLogProcess(containerName);
        logProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _ = _output.AppendAsync(workspaceId, appName, e.Data);
        };
        logProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _ = _output.AppendAsync(workspaceId, appName, "[err] " + e.Data);
        };
        logProcess.Exited += (sender, args) =>
        {
            _processes.TryRemove((workspaceId, appName), out var exited);
            exited?.Dispose();

            // Only update state for natural exits (not kills we initiated)
            if (!_containerNames.ContainsKey((workspaceId, appName)))
                return;

            _ = Task.Run(async () =>
            {
                var containerExitCode = await _docker.GetExitCodeAsync(containerName);
                _containerNames.TryRemove((workspaceId, appName), out _);
                await _docker.RunAsync("rm", "-f", containerName);

                var exitState = containerExitCode == 0 ? ApplicationState.Stopped : ApplicationState.Failed;
                await _registry.UpdateApplicationStateAsync(workspaceId, appName, exitState);
                _logger.LogInformation("Docker container for workspace application exited with code {Code}", containerExitCode);
            });
        };

        logProcess.Start();
        logProcess.BeginOutputReadLine();
        logProcess.BeginErrorReadLine();
        _processes[(workspaceId, appName)] = logProcess;
    }

    private async Task AppendDockerErrorAsync(string workspaceId, string appName, string stderr)
    {
        foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            await _output.AppendAsync(workspaceId, appName, "[err] " + line.TrimEnd('\r'));
    }

    public async Task KillAsync(string workspaceId)
    {
        var appNames = _processes.Keys
            .Where(k => k.Item1 == workspaceId)
            .Select(k => k.Item2)
            .Union(_containerNames.Keys
                .Where(k => k.Item1 == workspaceId)
                .Select(k => k.Item2))
            .ToList();

        foreach (var appName in appNames)
            await KillApplicationAsync(workspaceId, appName);
    }

    public async Task KillApplicationAsync(string workspaceId, string appName)
    {
        // Remove container tracking before killing so the Exited handler knows it was intentional
        _containerNames.TryRemove((workspaceId, appName), out var containerName);

        if (_processes.TryRemove((workspaceId, appName), out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    _localProcesses.Kill(process);
                    _logger.LogInformation("Killed workspace application process with pid {Pid}", process.Id);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                _logger.LogWarning(ex, "Failed to kill workspace application process");
            }

            process.Dispose();
        }

        if (containerName is not null)
        {
            _logger.LogInformation("Stopping Docker container for workspace application");
            await _docker.RunAsync("rm", "-f", containerName);
        }
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (workspaceId, appName) in _processes.Keys.ToList())
            _ = KillApplicationAsync(workspaceId, appName);
        return Task.CompletedTask;
    }
}
