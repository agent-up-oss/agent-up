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
        string? userDataDirectory = null)
    {
        var destination = _paths.EnsureUnderRoot(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var profile = string.IsNullOrWhiteSpace(userDataDirectory)
            ? null
            : _paths.EnsureUnderRoot(userDataDirectory);
        if (profile is not null)
            Directory.CreateDirectory(profile);
        var chromium = _environment.FindOnPath("chromium")
                       ?? _environment.FindOnPath("chromium-browser")
                       ?? _environment.FindOnPath("google-chrome");
        var workingDirectory = _paths.RepositoryRoot;
        var result = chromium is null
            ? await RunNixChromiumAsync(url, destination, workingDirectory, profile, cancellationToken)
            : await _processes.RunAsync(DirectCommand(chromium, url, destination, workingDirectory, profile), cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Chromium screenshot failed: {result.StandardError}");
        if (!File.Exists(destination))
            throw new InvalidOperationException("Chromium did not write a screenshot file.");
    }

    private static AllowlistedCommand DirectCommand(
        string chromium,
        string url,
        string destination,
        string workingDirectory,
        string? userDataDirectory)
        => new(
            Path.GetFileName(chromium),
            ChromiumArguments(url, destination, userDataDirectory),
            workingDirectory);

    private Task<ProcessResult> RunNixChromiumAsync(
        string url,
        string destination,
        string workingDirectory,
        string? userDataDirectory,
        CancellationToken cancellationToken)
        => _processes.RunAsync(
            new AllowlistedCommand(
                "nix-shell",
                [
                    "-p",
                    "chromium",
                    "--run",
                    $"chromium {string.Join(' ', ChromiumArguments(url, destination, userDataDirectory).Select(BashQuote.Single))}"
                ],
                workingDirectory),
            cancellationToken);

    private static string[] ChromiumArguments(string url, string destination, string? userDataDirectory)
    {
        var args = new List<string>
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            "--window-size=1440,900",
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
