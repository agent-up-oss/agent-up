using System.Collections.Concurrent;
using System.Diagnostics;
using AgentUp.Server.Features.Workspaces.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceProcessManager : IWorkspaceProcessManager, IHostedService
{
    private readonly ConcurrentDictionary<string, List<Process>> _processes = new();
    private readonly ILogger<WorkspaceProcessManager> _logger;

    public WorkspaceProcessManager(ILogger<WorkspaceProcessManager> logger)
    {
        _logger = logger;
    }

    public Task LaunchAsync(Workspace workspace)
    {
        KillExisting(workspace.Id);

        var started = new List<Process>();

        foreach (var app in workspace.Applications)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    ArgumentList = { "bash", "-c", app.Command },
                    WorkingDirectory = workspace.WorktreePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            started.Add(process);
            _logger.LogInformation("Started '{App}' (pid {Pid}) for workspace {Id}", app.Name, process.Id, workspace.Id);
        }

        _processes[workspace.Id] = started;
        return Task.CompletedTask;
    }

    public Task KillAsync(string workspaceId)
    {
        KillExisting(workspaceId);
        return Task.CompletedTask;
    }

    private void KillExisting(string workspaceId)
    {
        if (!_processes.TryRemove(workspaceId, out var processes))
            return;

        foreach (var p in processes)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    _logger.LogInformation("Killed pid {Pid} for workspace {Id}", p.Id, workspaceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill pid {Pid} for workspace {Id}", p.Id, workspaceId);
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var workspaceId in _processes.Keys.ToList())
            KillExisting(workspaceId);
        return Task.CompletedTask;
    }
}
