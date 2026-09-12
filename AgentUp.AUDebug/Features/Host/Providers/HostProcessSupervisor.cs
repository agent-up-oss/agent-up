using System.Diagnostics;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Host.Providers;

public sealed class HostProcessSupervisor : IHostProcessSupervisor
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugPathValidator _paths;
    private readonly IDebugEnvironment _environment;
    private readonly TextWriter _output;
    private readonly List<Process> _started = [];
    private readonly List<Task> _pumps = [];

    public HostProcessSupervisor(
        IAllowlistedProcessRunner processes,
        IDebugPathValidator paths,
        IDebugEnvironment environment,
        TextWriter output)
    {
        _processes = processes;
        _paths = paths;
        _environment = environment;
        _output = output;
    }

    public Task<HostSessionDto> StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_paths.LogsDirectory);
        var processes = new[]
        {
            Launch("server", ServerCommand(), DebugLayout.ServerUrl),
            Launch("desktop", DesktopCommand(), null),
            Launch("mobile", MobileCommand(), DebugLayout.MobileUrl),
            Launch("docs", DocsCommand(), DebugLayout.DocsUrl)
        };
        return Task.FromResult(new HostSessionDto(
            Environment.ProcessId,
            _paths.RepositoryRoot,
            _paths.SessionDirectory,
            processes));
    }

    public Task StopAsync(HostSessionDto session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var hosted in session.Processes)
            _processes.KillTree(hosted.Pid);
        if (session.SupervisorPid != Environment.ProcessId)
            _processes.KillTree(session.SupervisorPid);
        return Task.CompletedTask;
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _output.WriteLineAsync("au-debug: stopping");
        }
    }

    public bool HasLiveProcess(HostSessionDto session)
        => session.Processes.Any(process => _processes.IsRunning(process.Pid))
           || _processes.IsRunning(session.SupervisorPid);

    private HostedProcessDto Launch(string name, AllowlistedCommand command, string? url)
    {
        var logPath = _paths.EnsureUnderRoot(Path.Join(_paths.LogsDirectory, $"{name}.log"));
        var process = _processes.Start(command);
        _started.Add(process);
        _pumps.Add(PumpAsync(process, name, logPath));
        return new HostedProcessDto(name, process.Id, logPath, url);
    }

    private async Task PumpAsync(Process process, string name, string logPath)
    {
        using var log = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(log) { AutoFlush = true };
        await Task.WhenAll(
            CopyAsync(process.StandardOutput, name, writer),
            CopyAsync(process.StandardError, name, writer));
    }

    private async Task CopyAsync(StreamReader reader, string name, StreamWriter log)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                return;

            var prefixed = $"[{name}] {line}";
            await _output.WriteLineAsync(prefixed);
            await log.WriteLineAsync(prefixed);
        }
    }

    private AllowlistedCommand ServerCommand()
        => new(
            "dotnet",
            ["run", "--project", Path.Join(_paths.RepositoryRoot, "AgentUp.Server"), "--urls", DebugLayout.ServerUrl],
            _paths.RepositoryRoot,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = DebugLayout.ServerUrl
            });

    private AllowlistedCommand DesktopCommand()
        => new(
            "bash",
            [_paths.JoinUnderRoot("run-desktop.sh")],
            _paths.RepositoryRoot,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AGENTUP_SERVER_URL"] = DebugLayout.ServerUrl,
                ["DISPLAY"] = _environment.Display
            });

    private AllowlistedCommand MobileCommand()
        => new(
            "npm",
            ["run", "serve:web"],
            _paths.JoinUnderRoot("AgentUp.Mobile"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WEB_PORT"] = "10102"
            });

    private AllowlistedCommand DocsCommand()
        => new(
            "npm",
            ["run", "start", "--", "--host", "0.0.0.0", "--port", "10100"],
            _paths.JoinUnderRoot("docs"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BROWSER"] = "none",
                ["PORT"] = "10100"
            });
}
