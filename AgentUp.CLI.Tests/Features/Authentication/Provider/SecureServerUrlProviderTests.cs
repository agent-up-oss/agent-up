using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class SecureServerUrlProviderTests
{
    [Test]
    public void ResolveServerUri_allowsLoopbackHttp()
    {
        var uri = SecureServerUrlProvider.ResolveServerUri("http://localhost:5000");
        Assert.That(uri.ToString(), Is.EqualTo("http://localhost:5000/"));
    }

    [Test]
    public void ResolveServerUri_allowsRemoteHttps()
    {
        var uri = SecureServerUrlProvider.ResolveServerUri("https://agent-up.example.com");
        Assert.That(uri.Host, Is.EqualTo("agent-up.example.com"));
    }

    [Test]
    public void ResolveServerUri_rejectsRemoteHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SecureServerUrlProvider.ResolveServerUri("http://192.168.1.10:5000"));
    }
}
