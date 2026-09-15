namespace AgentUp.Tests.Fixtures.MacOs;

public sealed class MacOsDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    private DirectoryInfo? _fixtureHome;
    private string? _originalHome;
    private string? _originalTmpDir;
    public string Name => "AgentUp.Fixtures.MacOs";
    public bool RequiresStaThread => false;
    public bool RequiresSetupThreadAvalonia => true;
    public string StartupFailureHint => "macOS E2E tests require the GitHub runner's WindowServer-backed desktop session.";

    public void SetUp()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The macOS desktop fixture can only run on macOS.");
        if (!Environment.UserInteractive)
            throw new InvalidOperationException(StartupFailureHint);

        _fixtureHome = Directory.CreateTempSubdirectory("agentup-e2e-macos-");
        _originalHome = Environment.GetEnvironmentVariable("HOME");
        _originalTmpDir = Environment.GetEnvironmentVariable("TMPDIR");
        Environment.SetEnvironmentVariable("HOME", _fixtureHome.FullName);
        Environment.SetEnvironmentVariable("TMPDIR", _fixtureHome.FullName);
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "macos");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", _originalHome);
        Environment.SetEnvironmentVariable("TMPDIR", _originalTmpDir);
        _fixtureHome?.Delete(recursive: true);
    }

}
