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
    public void Windows_fixture_isolates_webview_storage()
    {
        var webViewData = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");

        Assert.Multiple(() =>
        {
            Assert.That(Environment.GetEnvironmentVariable("AGENTUP_E2E_PLATFORM"), Is.EqualTo("windows"));
            Assert.That(webViewData, Does.Contain("agentup-e2e-windows-"));
            Assert.That(Directory.Exists(webViewData), Is.True);
        });
    }

    // The fixture must not move the profile out from under the WebView2 browser process.
    // Asserting it leaves these alone is the only thing standing between a green Windows run
    // and the silent UI-thread hang that redirecting them causes.
    [Test]
    [Platform(Include = "Win")]
    public void Windows_fixture_leaves_the_real_profile_locations_alone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty, Does.Not.Contain("agentup-e2e-windows-"));
            Assert.That(Environment.GetEnvironmentVariable("APPDATA") ?? string.Empty, Does.Not.Contain("agentup-e2e-windows-"));
        });
    }
}
