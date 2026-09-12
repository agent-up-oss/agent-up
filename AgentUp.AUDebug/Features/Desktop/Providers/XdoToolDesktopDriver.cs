using AgentUp.AUDebug.Features.Desktop.Interfaces;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Desktop.Providers;

public sealed class XdoToolDesktopDriver : IDesktopWindowDriver
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public XdoToolDesktopDriver(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task WaitForWindowAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var windowId = await FindWindowIdAsync(cancellationToken);
            if (windowId is not null)
                return;

            await Task.Delay(250, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<bool> HasWindowAsync(CancellationToken cancellationToken)
        => await FindWindowIdAsync(cancellationToken) is not null;

    public async Task CaptureAsync(string outputPath, CancellationToken cancellationToken)
    {
        var windowId = await RequireWindowAsync(cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["windowactivate", "--sync", windowId], cancellationToken);
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var result = await RunToolAsync(
            "import",
            "imagemagick",
            ["-window", windowId, destination],
            cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Desktop screenshot failed: {result.StandardError}");
    }

    public async Task LoginAsync(string password, CancellationToken cancellationToken)
    {
        var windowId = await RequireWindowAsync(cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["windowactivate", "--sync", windowId], cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["mousemove", "--window", windowId, "640", "420"], cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["click", "1"], cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["type", "--clearmodifiers", "--file", "-"], cancellationToken, password);
        await RunToolAsync("xdotool", "xdotool", ["mousemove", "--window", windowId, "640", "520"], cancellationToken);
        await RunToolAsync("xdotool", "xdotool", ["click", "1"], cancellationToken);
    }

    private async Task<string> RequireWindowAsync(CancellationToken cancellationToken)
        => await FindWindowIdAsync(cancellationToken)
           ?? throw new InvalidOperationException("The Agent-Up Desktop window was not found. Run `au-debug up` first.");

    private async Task<string?> FindWindowIdAsync(CancellationToken cancellationToken)
    {
        var result = await RunToolAsync(
            "xdotool",
            "xdotool",
            ["search", "--class", DebugLayout.DesktopWindowClass],
            cancellationToken);
        var line = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
    }

    private async Task<ProcessResult> RunToolAsync(
        string executable,
        string nixPackage,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? standardInput = null)
    {
        var display = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = _environment.Display
        };
        var found = _environment.FindOnPath(executable);
        if (found is not null)
        {
            return await _processes.RunAsync(
                new AllowlistedCommand(executable, arguments, _paths.RepositoryRoot, display, standardInput),
                cancellationToken);
        }

        var quoted = string.Join(' ', arguments.Select(BashQuote.Single));
        var command = $"{executable} {quoted}";
        return await _processes.RunAsync(
            new AllowlistedCommand(
                "nix-shell",
                ["-p", nixPackage, "--run", command],
                _paths.RepositoryRoot,
                display,
                standardInput),
            cancellationToken);
    }
}
