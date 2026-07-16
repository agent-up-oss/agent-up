using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Processes.Services;

public sealed partial class WorkspaceProcessManager : IWorkspaceProcessManager, IHostedService
{
    // key: (workspaceId, appName)
    private readonly ConcurrentDictionary<(string, string), Process> _processes = new();
    private readonly ConcurrentDictionary<(string, string), string> _containerNames = new();

    private readonly WorkspaceRegistry _registry;
    private readonly IOutputRepository _output;
    private readonly ILogger<WorkspaceProcessManager> _logger;

    public WorkspaceProcessManager(
        WorkspaceRegistry registry,
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

        if (app.ServiceType == ServiceType.Docker)
        {
            await LaunchDockerServiceAsync(workspace.Id, app);
            return;
        }

        var startInfo = CreateLocalProcessStartInfo(workspace, app);

        var process = new Process
        {
            StartInfo = startInfo,
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

    private async Task LaunchDockerServiceAsync(string workspaceId, ApplicationInstance app)
    {
        var containerName = GetContainerName(workspaceId, app.Name);
        _containerNames[(workspaceId, app.Name)] = containerName;

        try
        {
            // Remove any stale container with this name
            await RunDockerAsync("rm", "-f", containerName);

            var runArgs = new List<string> { "run", "-d", "--name", containerName };
            foreach (var mapping in app.AllocatedPorts)
            {
                runArgs.Add("-p");
                runArgs.Add($"{mapping.AllocatedPort}:{mapping.DefaultPort}");
            }
            foreach (var (key, value) in app.Environment ?? new Dictionary<string, string>())
            {
                runArgs.Add("-e");
                runArgs.Add($"{key}={value}");
            }
            foreach (var volume in app.Volumes ?? [])
            {
                runArgs.Add("-v");
                runArgs.Add(volume);
            }
            runArgs.Add(app.Image!);

            var (exitCode, _, stderr) = await RunDockerAsync([.. runArgs]);
            if (exitCode != 0)
            {
                foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    await _output.AppendAsync(workspaceId, app.Name, "[err] " + line.TrimEnd('\r'));
                throw new InvalidOperationException($"docker run failed for '{app.Name}': {stderr.Trim()}");
            }

            _logger.LogInformation("Started Docker container '{Container}' for '{App}' in workspace {Id}", containerName, app.Name, workspaceId);
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
        catch
        {
            _containerNames.TryRemove((workspaceId, app.Name), out _);
            throw;
        }

        // Tail logs for output capture
        var logProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        logProcess.StartInfo.ArgumentList.Add("logs");
        logProcess.StartInfo.ArgumentList.Add("-f");
        logProcess.StartInfo.ArgumentList.Add(containerName);

        var appName = app.Name;
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
                var containerExitCode = await GetContainerExitCodeAsync(containerName);
                _containerNames.TryRemove((workspaceId, appName), out _);
                await RunDockerAsync("rm", "-f", containerName);

                var exitState = containerExitCode == 0 ? ApplicationState.Stopped : ApplicationState.Failed;
                await _registry.UpdateApplicationStateAsync(workspaceId, appName, exitState);
                _logger.LogInformation("Container '{Container}' for '{App}' in workspace {Id} exited with code {Code}", containerName, appName, workspaceId, containerExitCode);
            });
        };

        logProcess.Start();
        logProcess.BeginOutputReadLine();
        logProcess.BeginErrorReadLine();
        _processes[(workspaceId, appName)] = logProcess;
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
        }

        if (containerName is not null)
        {
            _logger.LogInformation("Stopping Docker container '{Container}' for '{App}' in workspace {Id}", containerName, appName, workspaceId);
            await RunDockerAsync("rm", "-f", containerName);
        }
    }

    private static async Task<int> GetContainerExitCodeAsync(string containerName)
    {
        var (_, stdout, _) = await RunDockerAsync("inspect", "--format={{.State.ExitCode}}", containerName);
        return int.TryParse(stdout.Trim(), out var code) ? code : 1;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDockerAsync(params string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"docker could not be started: {ex.Message}", ex);
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    internal static ProcessStartInfo CreateLocalProcessStartInfo(Workspace workspace, ApplicationInstance app)
    {
        var workingDirectory = app.Path is not null
            ? Path.Join(workspace.WorktreePath, app.Path)
            : workspace.WorktreePath;
        var startInfo = CreateShellStartInfo(app.Command!, workingDirectory);
        foreach (var mapping in workspace.Applications.SelectMany(a => a.AllocatedPorts))
        {
            if (mapping.Variable is not null)
                startInfo.Environment[mapping.Variable] = mapping.AllocatedPort.ToString();
        }

        return startInfo;
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/C");
        }
        else
        {
            startInfo.FileName = "/usr/bin/env";
            startInfo.ArgumentList.Add("bash");
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(command);
        return startInfo;
    }

    private static string GetContainerName(string workspaceId, string appName)
    {
        var safeId = workspaceId[..Math.Min(8, workspaceId.Length)];
        var safeName = ContainerNameSanitizer().Replace(appName.ToLower(), "-").Trim('-');
        return $"agentup-{safeId}-{safeName}";
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex ContainerNameSanitizer();

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (workspaceId, appName) in _processes.Keys.ToList())
            _ = KillApplicationAsync(workspaceId, appName);
        return Task.CompletedTask;
    }
}
