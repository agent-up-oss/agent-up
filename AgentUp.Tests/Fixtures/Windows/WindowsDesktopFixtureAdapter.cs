using AgentUp.Fixtures;

namespace AgentUp.Fixtures.Windows;

public sealed class WindowsDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    public string Name => "AgentUp.Fixtures.Windows";
    public bool RequiresStaThread => true;
    public string StartupFailureHint => "Windows E2E tests require the hosted runner's desktop session and WebView2 runtime.";

    public void SetUp()
    {
        Environment.SetEnvironmentVariable("AGENTUP_E2E_PLATFORM", "windows");
    }

    public void Dispose()
    {
    }
}
