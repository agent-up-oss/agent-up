using System.Diagnostics;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Brings up the one process a surface needs: the Desktop window, or the Mobile web host.
/// </summary>
/// <remarks>
/// Both are started directly rather than through <c>au-debug up</c>, which also starts a
/// Server and the docs site. Product screens run against the in-process Demo backend, so
/// those would be minutes of a CI job spent on things no screen shows.
/// <para>
/// The repository npm and Desktop entrypoints route through <c>nix-shell</c>, which a NixOS
/// workstation needs and a CI runner neither has nor wants: its replaced PATH would hide the
/// SDK the runner installed. Each command is therefore chosen from whether <c>nix-shell</c>
/// is on PATH, the same way Chromium is resolved.
/// </para>
/// </remarks>
public sealed class ScreenSurfaceHostProvider : IScreenSurfaceHost
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;
    private readonly IScreenReadyProbe _probe;
    private readonly List<Process> _started = [];
    private readonly List<Task> _drains = [];

    public ScreenSurfaceHostProvider(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths,
        IScreenReadyProbe probe)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
        _probe = probe;
    }

    public Task StartAsync(string surface, CancellationToken cancellationToken)
        => surface == ProductSurface.Desktop
            ? StartDesktopAsync(cancellationToken)
            : StartMobileAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        foreach (var process in _started)
        {
            _processes.KillTree(process.Id);
            process.Dispose();
        }

        _started.Clear();
        return ValueTask.CompletedTask;
    }

    private async Task StartDesktopAsync(CancellationToken cancellationToken)
    {
        if (await _probe.HasDesktopWindowAsync(cancellationToken))
            return;

        Launch("desktop", DesktopCommand());
        await _probe.WaitForDesktopWindowAsync(cancellationToken);
    }

    private async Task StartMobileAsync(CancellationToken cancellationToken)
    {
        if (await _probe.IsReadyAsync(DebugLayout.MobileUrl, cancellationToken))
            return;

        Launch("mobile", MobileCommand());
        await _probe.WaitForUrlAsync(DebugLayout.MobileUrl, cancellationToken);
    }

    /// <summary>
    /// Starts a host and drains it into a log file.
    /// </summary>
    /// <remarks>
    /// Draining is not optional. These are started with their output redirected, and a
    /// <c>dotnet run</c> that builds first writes more than a pipe holds, so a host nobody
    /// reads blocks before it ever opens a window - which looks exactly like a slow start
    /// until the watchdog fires with nothing to show for it.
    /// </remarks>
    private void Launch(string name, AllowlistedCommand command)
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        var logPath = _paths.EnsureUnderRoot(Path.Join(_paths.LogsDirectory, $"screens-{name}.log"));
        var process = _processes.Start(command);
        _started.Add(process);
        _drains.Add(DrainAsync(process, logPath));
    }

    private static async Task DrainAsync(Process process, string logPath)
    {
        using var file = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(file) { AutoFlush = true };
        await Task.WhenAll(
            CopyAsync(process.StandardOutput, writer),
            CopyAsync(process.StandardError, writer));
    }

    private static async Task CopyAsync(StreamReader reader, StreamWriter log)
    {
        while (await reader.ReadLineAsync() is { } line)
            await log.WriteLineAsync(line);
    }

    private AllowlistedCommand DesktopCommand()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = _environment.Display
        };
        if (_environment.FindOnPath("nix-shell") is not null)
            return new AllowlistedCommand("bash", [_paths.JoinUnderRoot("run-desktop.sh")], _paths.RepositoryRoot, environment);

        return new AllowlistedCommand(
            "dotnet",
            ["run", "--project", _paths.JoinUnderRoot("AgentUp.Desktop")],
            _paths.RepositoryRoot,
            environment);
    }

    private AllowlistedCommand MobileCommand()
    {
        var mobile = _paths.JoinUnderRoot("AgentUp.Mobile");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WEB_PORT"] = DebugLayout.MobileWebPort
        };
        if (_environment.FindOnPath("nix-shell") is not null)
            return new AllowlistedCommand("npm", ["run", "serve:web"], mobile, environment);

        return new AllowlistedCommand("node", ["scripts/serve-web.mjs"], mobile, environment);
    }
}
