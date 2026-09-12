using AgentUp.AUDebug.Features.Mobile.Interfaces;
using AgentUp.AUDebug.Shared.Interfaces;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeMobileSurfaceDriver : IMobileSurfaceDriver
{
    public string UserDataDirectory { get; set; } = "/tmp/chrome-mobile";
    public string? ServerUrl { get; private set; }
    public string? Password { get; private set; }
    public bool DelayUntilCanceled { get; set; }

    public async Task LoginAsync(string serverUrl, string password, CancellationToken cancellationToken)
    {
        ServerUrl = serverUrl;
        Password = password;
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

public sealed class FakeWebScreenshotDriver : IWebScreenshotDriver
{
    public List<(string Url, string Path, string? UserDataDirectory)> Captures { get; } = [];
    public bool DelayUntilCanceled { get; set; }
    public Exception? CaptureException { get; set; }

    public async Task CaptureAsync(string url, string outputPath, CancellationToken cancellationToken, string? userDataDirectory = null)
    {
        Captures.Add((url, outputPath, userDataDirectory));
        if (CaptureException is not null)
            throw CaptureException;
        if (DelayUntilCanceled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

public sealed class FakeEnvironment : IDebugEnvironment
{
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);
    public string Display { get; set; } = ":0";
    public string? AdminPassword { get; set; } = "test";
    public Dictionary<string, string> Executables { get; } = new(StringComparer.Ordinal);

    public string? GetVariable(string name)
        => Variables.TryGetValue(name, out var value) ? value : null;

    public string? FindOnPath(string executableName)
        => Executables.TryGetValue(executableName, out var path) ? path : null;
}

public sealed class FakePathValidator : IDebugPathValidator
{
    public FakePathValidator(string root)
    {
        RepositoryRoot = root;
        SessionDirectory = Path.Join(root, ".git", "agent-up", "au-debug");
        LogsDirectory = Path.Join(SessionDirectory, "logs");
        ScreenshotsDirectory = Path.Join(SessionDirectory, "screenshots");
    }

    public string RepositoryRoot { get; }
    public string SessionDirectory { get; }
    public string LogsDirectory { get; }
    public string ScreenshotsDirectory { get; }

    public string JoinUnderRoot(params string[] segments)
        => EnsureUnderRoot(Path.Join([RepositoryRoot, .. segments]));

    public string EnsureUnderRoot(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Path.GetFullPath(RepositoryRoot), StringComparison.Ordinal))
            throw new InvalidOperationException($"Path '{full}' is outside the repository root.");
        return full;
    }
}
