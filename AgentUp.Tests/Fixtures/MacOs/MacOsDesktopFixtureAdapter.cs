namespace AgentUp.Tests.Fixtures.MacOs;

public sealed class MacOsDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    public string Name => "AgentUp.Fixtures.MacOs";
    public bool RequiresStaThread => false;
    public bool RequiresSetupThreadAvalonia => true;
    public string StartupFailureHint => "macOS E2E tests require the GitHub runner's WindowServer-backed desktop session.";

    public void SetUp()
    {
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "macos");
    }

    public void Dispose()
    {
    }
}
