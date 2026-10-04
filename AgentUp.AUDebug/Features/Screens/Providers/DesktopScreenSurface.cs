using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Drives the real Desktop window on an X display and captures it with ImageMagick.
/// </summary>
/// <remarks>
/// <see cref="OpenAsync"/> pins the window to a known size and origin before the first step,
/// because every point in <see cref="DesktopScreenGeometry"/> was read at that size, and a
/// bare Xvfb has no window manager to honour the app's own request.
/// </remarks>
public sealed class DesktopScreenSurface : IProductScreenSurface
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;
    private string? _windowId;

    public DesktopScreenSurface(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public string Surface => ProductSurface.Desktop;

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        var window = await RequireWindowAsync(cancellationToken);
        await XdoToolAsync(["windowsize", window, $"{DebugLayout.DesktopScreenshotWidth}", $"{DebugLayout.DesktopScreenshotHeight}"], cancellationToken);
        await XdoToolAsync(["windowmove", window, "0", "0"], cancellationToken);
        await Task.Delay(1500, cancellationToken);
    }

    public async Task RunAsync(IReadOnlyList<ScreenStepDto> steps, CancellationToken cancellationToken)
    {
        foreach (var step in steps)
            await StepAsync(step, cancellationToken);
    }

    public async Task CaptureAsync(string outputPath, CancellationToken cancellationToken)
    {
        var window = await RequireWindowAsync(cancellationToken);
        var result = await RunToolAsync("import", "imagemagick", ["-window", ToX11WindowId(window), outputPath], cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Desktop screen capture failed: {result.StandardError}");
    }

    /// <summary>
    /// Desktop has no text channel: it is driven with xdotool and photographed with
    /// ImageMagick, neither of which can read the Avalonia visual tree. The comparison reports
    /// these screens as not comparable rather than passing them without checking anything.
    /// </summary>
    public Task<string?> ReadTextAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task StepAsync(ScreenStepDto step, CancellationToken cancellationToken)
    {
        var window = await RequireWindowAsync(cancellationToken);
        switch (step.Kind)
        {
            case ScreenStepKind.Click:
                await XdoToolAsync(["mousemove", "--window", window, $"{step.X}", $"{step.Y}"], cancellationToken);
                await XdoToolAsync(["click", "1"], cancellationToken);
                break;
            case ScreenStepKind.Type:
                await XdoToolAsync(["type", "--clearmodifiers", "--delay", "15", step.Text ?? ""], cancellationToken);
                break;
            case ScreenStepKind.Key:
                await XdoToolAsync(["key", "--clearmodifiers", step.Text ?? ""], cancellationToken);
                break;
            case ScreenStepKind.Settle:
                break;
            default:
                throw new InvalidOperationException($"Desktop screens cannot run a '{step.Kind}' step.");
        }

        await Task.Delay(step.DelayMs, cancellationToken);
    }

    private static string ToX11WindowId(string windowId)
        => windowId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? windowId
            : "0x" + ulong.Parse(windowId, System.Globalization.CultureInfo.InvariantCulture)
                .ToString("x", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<string> RequireWindowAsync(CancellationToken cancellationToken)
    {
        if (_windowId is not null)
            return _windowId;

        var result = await XdoToolAsync(["search", "--onlyvisible", "--class", DebugLayout.DesktopWindowClass], cancellationToken);
        var line = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        _windowId = string.IsNullOrWhiteSpace(line)
            ? throw new InvalidOperationException("The Agent-Up Desktop window was not found while capturing product screens.")
            : line.Trim();
        return _windowId;
    }

    private Task<ProcessResult> XdoToolAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunToolAsync("xdotool", "xdotool", arguments, cancellationToken);

    private Task<ProcessResult> RunToolAsync(
        string executable,
        string nixPackage,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
        => X11ToolRunner.RunAsync(
            _processes,
            _environment,
            _paths.RepositoryRoot,
            executable,
            nixPackage,
            arguments,
            cancellationToken);
}
