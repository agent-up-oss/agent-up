namespace AgentUp.Tests.Features.Browser.E2E;

[TestFixture, Category("E2E")]
public sealed class NativeDesktopFixtureIsolationTests
{
    [Test]
    [Platform(Include = "MacOsX")]
    public void MacOs_fixture_isolates_home_and_temporary_storage()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var temporary = Environment.GetEnvironmentVariable("TMPDIR");

        Assert.Multiple(() =>
        {
            Assert.That(Environment.GetEnvironmentVariable("AGENTUP_E2E_PLATFORM"), Is.EqualTo("macos"));
            Assert.That(home, Does.Contain("agentup-e2e-macos-"));
            Assert.That(temporary, Is.EqualTo(home));
            Assert.That(Directory.Exists(home), Is.True);
        });
    }

    [Test]
    [Platform(Include = "Win")]
    public void Windows_fixture_isolates_application_and_webview_storage()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        var webViewData = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");

        Assert.Multiple(() =>
        {
            Assert.That(Environment.GetEnvironmentVariable("AGENTUP_E2E_PLATFORM"), Is.EqualTo("windows"));
            Assert.That(localAppData, Does.Contain("agentup-e2e-windows-"));
            Assert.That(appData, Does.Contain("agentup-e2e-windows-"));
            Assert.That(webViewData, Does.Contain("agentup-e2e-windows-"));
            Assert.That(Directory.Exists(localAppData), Is.True);
            Assert.That(Directory.Exists(appData), Is.True);
            Assert.That(Directory.Exists(webViewData), Is.True);
        });
    }
}
