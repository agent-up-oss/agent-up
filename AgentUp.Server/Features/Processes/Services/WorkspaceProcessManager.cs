using System.Collections.Concurrent;
using System.Diagnostics;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Processes.Services;

public sealed partial class WorkspaceProcessManager : IWorkspaceProcessManager, IHostedService
{
    // key: (workspaceId, appName)
    private readonly ConcurrentDictionary<(string, string), Process> _processes = new();
    private readonly ConcurrentDictionary<(string, string), string> _containerNames = new();

    private readonly WorkspaceStateController _registry;
    private readonly ProcessOutputService _output;
    private readonly ILocalProcessProvider _localProcesses;
    private readonly IDockerProcessProvider _docker;
    private readonly ILogger<WorkspaceProcessManager> _logger;
    private readonly DesktopApplicationsController? _desktopApplications;

    public WorkspaceProcessManager(
        WorkspaceStateController registry,
        ProcessOutputService output,
        ILocalProcessProvider localProcesses,
        IDockerProcessProvider docker,
        ILogger<WorkspaceProcessManager> logger,
        DesktopApplicationsController? desktopApplications = null)
    {
        _registry = registry;
        _output = output;
        _localProcesses = localProcesses;
        _docker = docker;
        _logger = logger;
        _desktopApplications = desktopApplications;
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
                await _output.AppendAsync(workspace.Id, appName, "[err] " + message, ProcessOutputStream.Stderr);

            await _registry.UpdateApplicationStateAsync(workspace.Id, appName, ApplicationState.Failed);
            throw new InvalidOperationException($"Capability '{app.CapabilityStatus.CapabilityId}' cannot run '{appName}'.");
        }

        if (app.ServiceType == ServiceType.Docker)
        {
            await LaunchDockerServiceAsync(workspace, app);
            return;
        }

        await RunInstallStepAsync(workspace, app);

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
                _ = _output.AppendAsync(workspaceId, appName, "[err] " + e.Data, ProcessOutputStream.Stderr);
        };

        process.Exited += (sender, args) =>
        {
            var key = (workspaceId, appName);
            if (!_processes.TryRemove(key, out var exited))
                return; // KillApplicationAsync already removed this process; it manages the final state
            var exitCode = (sender as Process)?.ExitCode ?? -1;
            var exitState = exitCode == 0 ? ApplicationState.Stopped : ApplicationState.Failed;
            _ = _registry.UpdateApplicationStateAsync(workspaceId, appName, exitState);
            if (app.Kind == ApplicationKind.Desktop)
                _ = _desktopApplications?.StopAsync(workspaceId, appName, CancellationToken.None);
            _logger.LogInformation("Workspace application process exited with code {Code}", exitCode);
            exited.Dispose();
        };

        var key = (workspaceId, appName);
        _processes[key] = process;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _processes.TryRemove(key, out _);
            process.Dispose();
            await _output.AppendAsync(workspaceId, appName, "[err] " + ex.Message, ProcessOutputStream.Stderr);
            throw new InvalidOperationException($"Failed to start '{appName}': {ex.Message}", ex);
        }

        _logger.LogInformation("Started workspace application process with pid {Pid}", process.Id);
    }

    // Install commands (npm install, dotnet restore, etc.) are idempotent by design, so
    // running them unconditionally before every launch keeps dependencies current without
    // needing a completion marker to decide whether a run is "needed".
    private async Task RunInstallStepAsync(Workspace workspace, ApplicationInstance app)
    {
        var workspaceId = workspace.Id;
        var appName = app.Name;
        var key = (workspaceId, appName);

        // CreateInstallProcess parses and validates app.Install (allowlist, shell-expression
        // rejection) before any process exists, so a bad command must be treated as an install
        // failure here too, not left to surface as an unhandled exception with no app state update.
        Process? created;
        try
        {
            created = _localProcesses.CreateInstallProcess(workspace, app);
        }
        catch (InvalidOperationException ex)
        {
            await _output.AppendAsync(workspaceId, appName, "[install] [err] " + ex.Message, ProcessOutputStream.Stderr);
            await _registry.UpdateApplicationStateAsync(workspaceId, appName, ApplicationState.Failed);
            throw;
        }

        if (created is null)
            return;

        using var install = created;

        // WaitForExitAsync only guarantees the process itself has exited, not that queued
        // OutputDataReceived/ErrorDataReceived events have been raised or that the AppendAsync
        // writes they start have completed; pendingOutput tracks those writes so they can be
        // awaited before anything downstream (exit-code check, application launch) proceeds.
        var pendingOutput = new ConcurrentBag<Task>();

        install.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                pendingOutput.Add(_output.AppendAsync(workspaceId, appName, "[install] " + e.Data));
        };

        install.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                pendingOutput.Add(_output.AppendAsync(workspaceId, appName, "[install] [err] " + e.Data, ProcessOutputStream.Stderr));
        };

        // Registering under the same key the application process later uses lets a concurrent
        // KillApplicationAsync (a Stop while this Start is still installing) actually terminate
        // the install, instead of leaving it to finish unsupervised and launch the application
        // command anyway.
        _processes[key] = install;

        int exitCode;
        try
        {
            install.Start();
            install.BeginOutputReadLine();
            install.BeginErrorReadLine();
            await install.WaitForExitAsync();
            // Flushes any OutputDataReceived/ErrorDataReceived events still in flight now that
            // the process has exited, per the documented WaitForExitAsync + WaitForExit pairing.
            install.WaitForExit();
            exitCode = install.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _processes.TryRemove(key, out _);
            await _output.AppendAsync(workspaceId, appName, "[install] [err] " + ex.Message, ProcessOutputStream.Stderr);
            await _registry.UpdateApplicationStateAsync(workspaceId, appName, ApplicationState.Failed);
            throw new InvalidOperationException($"Install step failed for '{appName}': {ex.Message}", ex);
        }

        await Task.WhenAll(pendingOutput);

        // If KillApplicationAsync already claimed this key, it owns the resulting application
        // state (and disposal) for this Stop; don't overwrite that with Failed or proceed to
        // launch the application command behind its back.
        if (!_processes.TryRemove(key, out var owned) || !ReferenceEquals(owned, install))
            throw new InvalidOperationException($"Application '{appName}' was stopped before its install step finished.");

        if (exitCode != 0)
        {
            await _registry.UpdateApplicationStateAsync(workspaceId, appName, ApplicationState.Failed);
            throw new InvalidOperationException($"Install step for '{appName}' exited with code {exitCode}.");
        }

        _logger.LogInformation("Install step for workspace application completed");
    }

    private async Task LaunchDockerServiceAsync(Workspace workspace, ApplicationInstance app)
    {
        var workspaceId = workspace.Id;
        var containerName = _docker.GetContainerName(workspaceId, app.Name);
        _containerNames[(workspaceId, app.Name)] = containerName;

        try
        {
            // Remove any stale container with this name. rm -f is a no-op if none exists.
            await _docker.RunAsync("rm", "-f", containerName);

            var runArgs = _docker.CreateRunArguments(containerName, workspace, app).ToArray();
            var run = await _docker.RunAsync(runArgs);

            // Docker daemon occasionally still holds the name for a moment after an
            // rm -f — a subsequent run then fails with "already in use". Force-remove
            // one more time and retry the run so the user doesn't see a confusing
            // conflict message caused by daemon-side timing.
            if (run.ExitCode != 0
                && run.Stderr.Contains("is already in use", StringComparison.OrdinalIgnoreCase))
            {
                await _docker.RunAsync("rm", "-f", containerName);
                run = await _docker.RunAsync(runArgs);
            }

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
            await _output.AppendAsync(workspaceId, app.Name, "[err] " + ex.Message, ProcessOutputStream.Stderr);
            throw new InvalidOperationException($"docker failed for '{app.Name}': {ex.Message}", ex);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is System.ComponentModel.Win32Exception)
        {
            _containerNames.TryRemove((workspaceId, app.Name), out _);
            await _output.AppendAsync(workspaceId, app.Name, "[err] " + ex.Message, ProcessOutputStream.Stderr);
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
                _ = _output.AppendAsync(workspaceId, appName, "[err] " + e.Data, ProcessOutputStream.Stderr);
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
            await _output.AppendAsync(workspaceId, appName, "[err] " + line.TrimEnd('\r'), ProcessOutputStream.Stderr);
    }

    public WorkspaceRuntimeSnapshot GetRuntime(string workspaceId)
    {
        var samples = _processes
            .Where(entry => entry.Key.Item1 == workspaceId)
            .Select(entry => TryReadRuntime(entry.Value))
            .Where(sample => sample is not null)
            .Select(sample => sample!)
            .ToList();

        return new WorkspaceRuntimeSnapshot(
            samples.Sum(sample => sample.CpuPercent),
            samples.Sum(sample => sample.MemoryBytes),
            samples.Count);
    }

    private static WorkspaceRuntimeSnapshot? TryReadRuntime(Process process)
    {
        try
        {
            process.Refresh();
            if (process.HasExited)
                return null;

            var memoryBytes = process.WorkingSet64;
            var elapsedMilliseconds = (DateTime.Now - process.StartTime).TotalMilliseconds;
            var cpuPercent = elapsedMilliseconds > 0
                ? process.TotalProcessorTime.TotalMilliseconds / elapsedMilliseconds / Environment.ProcessorCount * 100
                : 0;
            return new WorkspaceRuntimeSnapshot(cpuPercent, memoryBytes, 1);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
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
        var key = (workspaceId, appName);
        _containerNames.TryRemove(key, out var containerName);

        if (_processes.TryRemove(key, out var process))
        {
            try
            {
                if (process.HasExited)
                {
                    await _registry.UpdateApplicationStateAsync(workspaceId, appName, StateFromExitCode(process.ExitCode));
                }
                else
                {
                    _localProcesses.Kill(process);
                    _logger.LogInformation("Killed workspace application process with pid {Pid}", process.Id);
                    await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(TimeSpan.FromSeconds(5)));
                    await _registry.UpdateApplicationStateAsync(workspaceId, appName, ApplicationState.Stopped);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // HasExited and ExitCode also throw when the process loses its native handle
                // during a concurrent launch/kill. Do not probe the same invalid handle again.
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

    private static ApplicationState StateFromExitCode(int exitCode)
        => exitCode == 0 ? ApplicationState.Stopped : ApplicationState.Failed;

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (workspaceId, appName) in _processes.Keys.ToList())
            _ = KillApplicationAsync(workspaceId, appName);
        return Task.CompletedTask;
    }
}
