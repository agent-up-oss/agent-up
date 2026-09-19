namespace AgentUp.Server.Tests.Support;

internal sealed class TestGitConfigIsolation : IDisposable
{
    private readonly string _directory;
    private readonly string? _previousGlobal;
    private readonly string? _previousSystem;
    private readonly string? _previousNoSystem;

    private TestGitConfigIsolation()
    {
        _directory = Path.Join(Path.GetTempPath(), $"git-isolate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        var empty = Path.Join(_directory, "empty.gitconfig");
        File.WriteAllText(empty, string.Empty);
        _previousGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        _previousSystem = Environment.GetEnvironmentVariable("GIT_CONFIG_SYSTEM");
        _previousNoSystem = Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", empty);
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", empty);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
    }

    public static TestGitConfigIsolation Begin() => new();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", _previousGlobal);
        Environment.SetEnvironmentVariable("GIT_CONFIG_SYSTEM", _previousSystem);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", _previousNoSystem);
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
