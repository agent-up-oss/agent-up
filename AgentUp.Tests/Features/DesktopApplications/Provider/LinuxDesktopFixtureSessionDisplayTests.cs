using AgentUp.Tests.Fixtures.Linux;

namespace AgentUp.Tests.Features.DesktopApplications.Provider;

/// <summary>
/// The session-display opt-in is a maintainer debug switch. Isolation checks must follow that
/// decision instead of skipping, and both outcomes have to be visible on any host.
/// </summary>
[TestFixture]
public sealed class LinuxDesktopFixtureSessionDisplayTests
{
    [Test]
    public void A_ready_session_display_opts_out_of_isolation()
    {
        var isolate = LinuxDesktopFixtureAdapter.RequiresIsolationAssertions(
            name => name switch
            {
                LinuxDesktopFixtureAdapter.SessionDisplayFlag => "1",
                "DISPLAY" => ":0",
                _ => null
            },
            () => true);

        Assert.That(isolate, Is.False);
    }

    [Test]
    public void An_opt_in_without_a_ready_display_keeps_isolation()
    {
        var isolate = LinuxDesktopFixtureAdapter.RequiresIsolationAssertions(
            name => name == LinuxDesktopFixtureAdapter.SessionDisplayFlag ? "1" : ":0",
            () => false);

        Assert.That(isolate, Is.True);
    }

    [Test]
    public void A_missing_opt_in_keeps_isolation_even_when_a_display_is_ready()
    {
        var isolate = LinuxDesktopFixtureAdapter.RequiresIsolationAssertions(
            name => name == "DISPLAY" ? ":0" : null,
            () => true);

        Assert.That(isolate, Is.True);
    }

    [Test]
    public void A_blank_display_keeps_isolation()
    {
        var isolate = LinuxDesktopFixtureAdapter.RequiresIsolationAssertions(
            name => name == LinuxDesktopFixtureAdapter.SessionDisplayFlag ? "1" : " ",
            () => true);

        Assert.That(isolate, Is.True);
    }
}
