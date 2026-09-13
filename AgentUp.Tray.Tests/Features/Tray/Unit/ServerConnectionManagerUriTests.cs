using AgentUp.Tray.Features.Tray;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class ServerConnectionManagerUriTests
{
    private static readonly Uri LocalDefault = new("http://127.0.0.1:5000");

    [Test]
    public void ResolveServerUri_usesAConfiguredAbsoluteUrl()
    {
        Assert.That(ServerConnectionManager.ResolveServerUri("http://192.168.1.10:5100"),
            Is.EqualTo(new Uri("http://192.168.1.10:5100")));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ResolveServerUri_fallsBackToTheLocalDefaultWhenUnset(string? configured)
    {
        Assert.That(ServerConnectionManager.ResolveServerUri(configured), Is.EqualTo(LocalDefault));
    }

    [Test]
    public void ResolveServerUri_fallsBackForAHostPortValueWithNoScheme()
    {
        // Uri.TryCreate accepts this as absolute with "localhost" as the scheme, so
        // absoluteness alone is not enough to trust the value.
        Assert.That(ServerConnectionManager.ResolveServerUri("localhost:5000"), Is.EqualTo(LocalDefault));
    }

    [TestCase("ftp://example.com")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ws://127.0.0.1:5000")]
    public void ResolveServerUri_fallsBackForANonHttpScheme(string configured)
    {
        Assert.That(ServerConnectionManager.ResolveServerUri(configured), Is.EqualTo(LocalDefault));
    }

    [Test]
    public void ResolveServerUri_acceptsHttps()
    {
        Assert.That(ServerConnectionManager.ResolveServerUri("https://agent-up.example.com"),
            Is.EqualTo(new Uri("https://agent-up.example.com")));
    }

    [Test]
    public void ResolveServerUri_fallsBackWhenTheConfiguredValueIsNotAUrl()
    {
        Assert.That(ServerConnectionManager.ResolveServerUri("nonsense"), Is.EqualTo(LocalDefault));
    }

    [Test]
    public void Constructor_fromAUriContactsNothingUntilStarted()
    {
        // The production wiring goes through this overload. Constructing it must not reach
        // the address, so the state stays Connecting until a poll actually runs.
        using var manager = new ServerConnectionManager(new Uri("http://192.0.2.1:5100"));

        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Connecting));
    }

    [Test]
    public void Constructor_withoutArgumentsResolvesTheConfiguredServerWithoutContactingIt()
    {
        // The tray builds this overload at startup. It reads AGENTUP_SERVER_URL and falls
        // back to the local default, and must do so without a server being up.
        using var manager = new ServerConnectionManager();

        Assert.That(manager.CurrentState, Is.EqualTo(ServiceState.Connecting));
    }
}
