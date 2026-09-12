using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Shared.Providers;

public sealed class ChromiumScreenshotDriver : IWebScreenshotDriver
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public ChromiumScreenshotDriver(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task CaptureAsync(string url, string outputPath, CancellationToken cancellationToken)
    {
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var chromium = _environment.FindOnPath("chromium")
                       ?? _environment.FindOnPath("chromium-browser")
                       ?? _environment.FindOnPath("google-chrome");
        var workingDirectory = _paths.RepositoryRoot;
        var result = chromium is null
            ? await RunNixChromiumAsync(url, destination, workingDirectory, cancellationToken)
            : await _processes.RunAsync(DirectCommand(chromium, url, destination, workingDirectory), cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Chromium screenshot failed: {result.StandardError}");
        if (!File.Exists(destination))
            throw new InvalidOperationException("Chromium did not write a screenshot file.");
    }

    private static AllowlistedCommand DirectCommand(string chromium, string url, string destination, string workingDirectory)
        => new(
            Path.GetFileName(chromium),
            [
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--hide-scrollbars",
                "--window-size=1440,900",
                "--virtual-time-budget=8000",
                "--run-all-compositor-stages-before-draw",
                $"--screenshot={destination}",
                "--timeout=25000",
                url
            ],
            workingDirectory);

    private Task<ProcessResult> RunNixChromiumAsync(string url, string destination, string workingDirectory, CancellationToken cancellationToken)
        => _processes.RunAsync(
            new AllowlistedCommand(
                "nix-shell",
                [
                    "-p",
                    "chromium",
                    "--run",
                    $"chromium --headless=new --disable-gpu --no-sandbox --hide-scrollbars --window-size=1440,900 --virtual-time-budget=8000 --run-all-compositor-stages-before-draw --screenshot={BashQuote.Single(destination)} --timeout=25000 {BashQuote.Single(url)}"
                ],
                workingDirectory),
            cancellationToken);
}
