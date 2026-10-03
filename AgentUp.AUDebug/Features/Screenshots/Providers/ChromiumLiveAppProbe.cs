using AgentUp.AUDebug.Features.Screenshots.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Screenshots.Providers;

public sealed class ChromiumLiveAppProbe : IScreenshotLiveAppProbe
{
    private readonly IAllowlistedProcessRunner _processes;
    private readonly IDebugEnvironment _environment;
    private readonly IDebugPathValidator _paths;

    public ChromiumLiveAppProbe(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        IDebugPathValidator paths)
    {
        _processes = processes;
        _environment = environment;
        _paths = paths;
    }

    public async Task<string> ReadPageTextAsync(string url, CancellationToken cancellationToken)
    {
        var chromium = _environment.FindChromium();
        var args = new[]
        {
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--dump-dom",
            "--virtual-time-budget=8000",
            url
        };
        var command = chromium is null
            ? new AllowlistedCommand(
                "nix-shell",
                ["-p", "chromium", "--run", $"chromium {string.Join(' ', args.Select(BashQuote.Single))}"],
                _paths.RepositoryRoot)
            : new AllowlistedCommand(Path.GetFileName(chromium), args, _paths.RepositoryRoot);
        var result = await _processes.RunAsync(command, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Chromium dump-dom failed: {result.StandardError}");
        return result.StandardOutput;
    }
}
