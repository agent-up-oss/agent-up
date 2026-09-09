using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class SecureServerUrlProviderTests
{
    [Test]
    public void ResolveServerUri_AllowsLoopbackHttp()
    {
        var uri = SecureServerUrlProvider.ResolveServerUri("http://localhost:5000");
        Assert.That(uri.Host, Is.EqualTo("localhost"));
    }

    [Test]
    public void ResolveServerUri_AllowsRemoteHttps()
    {
        var uri = SecureServerUrlProvider.ResolveServerUri("https://agent-up.example.com");
        Assert.That(uri.Host, Is.EqualTo("agent-up.example.com"));
    }

    [Test]
    public void ResolveServerUri_RejectsRemoteHttp()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SecureServerUrlProvider.ResolveServerUri("http://192.168.1.10:5000"));
    }
}
