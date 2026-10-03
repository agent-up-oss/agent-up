using AgentUp.AUDebug.Shared.Interfaces;

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

    public async Task CaptureAsync(
        string url,
        string outputPath,
        CancellationToken cancellationToken,
        string? userDataDirectory = null,
        int width = 1440,
        int height = 900)
    {
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var profile = _paths.EnsureUnderRoot(
            string.IsNullOrWhiteSpace(userDataDirectory)
                ? Path.Join(_paths.ScreenshotsDirectory, "chrome-profile")
                : userDataDirectory);
        Directory.CreateDirectory(profile);
        var chromium = _environment.FindChromium();
        var workingDirectory = _paths.RepositoryRoot;
        try
        {
            var result = chromium is null
                ? await RunNixChromiumAsync(url, destination, workingDirectory, profile, width, height, cancellationToken)
                : await _processes.RunAsync(DirectCommand(chromium, url, destination, workingDirectory, profile, width, height), cancellationToken);

            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Chromium screenshot failed: {result.StandardError}");
            if (!File.Exists(destination))
                throw new InvalidOperationException("Chromium did not write a screenshot file.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not start Chromium: {ex.Message}");
        }
    }

    private static AllowlistedCommand DirectCommand(
        string chromium,
        string url,
        string destination,
        string workingDirectory,
        string? userDataDirectory,
        int width,
        int height)
        => new(
            Path.GetFileName(chromium),
            ChromiumArguments(url, destination, userDataDirectory, width, height),
            workingDirectory);

    private Task<ProcessResult> RunNixChromiumAsync(
        string url,
        string destination,
        string workingDirectory,
        string? userDataDirectory,
        int width,
        int height,
        CancellationToken cancellationToken)
        => _processes.RunAsync(
            new AllowlistedCommand(
                "nix-shell",
                [
                    "-p",
                    "chromium",
                    "--run",
                    $"chromium {string.Join(' ', ChromiumArguments(url, destination, userDataDirectory, width, height).Select(BashQuote.Single))}"
                ],
                workingDirectory),
            cancellationToken);

    private static string[] ChromiumArguments(
        string url,
        string destination,
        string? userDataDirectory,
        int width,
        int height)
    {
        var args = new List<string>
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            "--force-device-scale-factor=1",
            "--disable-lcd-text",
            "--font-render-hinting=none",
            $"--window-size={width},{height}",
            "--virtual-time-budget=8000",
            "--run-all-compositor-stages-before-draw",
            $"--screenshot={destination}",
            "--timeout=25000"
        };
        if (userDataDirectory is not null)
            args.Add($"--user-data-dir={userDataDirectory}");
        args.Add(url);
        return [.. args];
    }
}
