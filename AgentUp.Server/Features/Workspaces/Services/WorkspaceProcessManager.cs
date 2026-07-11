using System.Collections.Concurrent;
using System.Diagnostics;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceProcessManager : IWorkspaceProcessManager, IHostedService
{
    // key: (workspaceId, appName)
    private readonly ConcurrentDictionary<(string, string), Process> _processes = new();

    private readonly IWorkspaceRegistry _registry;
    private readonly IOutputRepository _output;
    private readonly ILogger<WorkspaceProcessManager> _logger;

    public WorkspaceProcessManager(
        IWorkspaceRegistry registry,
        IOutputRepository output,
        ILogger<WorkspaceProcessManager> logger)
    {
        _registry = registry;
        _output = output;
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

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/env",
                ArgumentList = { "bash", "-c", app.Command },
                WorkingDirectory = workspace.WorktreePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

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
            _logger.LogInformation("Process for '{App}' in workspace {Id} exited with code {Code}", appName, workspaceId, exitCode);
            exited?.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes[(workspaceId, appName)] = process;
        _logger.LogInformation("Started '{App}' (pid {Pid}) in workspace {Id}", appName, process.Id, workspaceId);
    }

    public async Task KillAsync(string workspaceId)
    {
        var appNames = _processes.Keys
            .Where(k => k.Item1 == workspaceId)
            .Select(k => k.Item2)
            .ToList();

        foreach (var appName in appNames)
            await KillApplicationAsync(workspaceId, appName);
    }

    public Task KillApplicationAsync(string workspaceId, string appName)
    {
        if (!_processes.TryRemove((workspaceId, appName), out var process))
            return Task.CompletedTask;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogInformation("Killed '{App}' (pid {Pid}) in workspace {Id}", appName, process.Id, workspaceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill '{App}' in workspace {Id}", appName, workspaceId);
        }
        finally
        {
            process.Dispose();
        }

        return Task.CompletedTask;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (workspaceId, appName) in _processes.Keys.ToList())
            KillApplicationAsync(workspaceId, appName);
        return Task.CompletedTask;
    }
}
