using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Tests.Features.Sessions.Unit;

[TestFixture]
public sealed class BrowserSessionStateKeyTests
{
    private static string Key(string url) => BrowserSessionState.PageKeyForUrl(new Uri(url));

    [Test]
    public void PageKeyForUrl_keysByOriginSoOnePageIsReusedPerSite()
    {
        Assert.That(Key("https://example.com/a/b?q=1"), Is.EqualTo("https://example.com"));
    }

    [Test]
    public void PageKeyForUrl_treatsDifferentPathsOnOneOriginAsTheSamePage()
    {
        Assert.That(Key("https://example.com/one"), Is.EqualTo(Key("https://example.com/two")));
    }

    [Test]
    public void PageKeyForUrl_separatesDifferentPorts()
    {
        Assert.That(Key("http://localhost:3000/"), Is.Not.EqualTo(Key("http://localhost:5601/")));
    }

    [Test]
    public void PageKeyForUrl_separatesSchemes()
    {
        Assert.That(Key("http://example.com/"), Is.Not.EqualTo(Key("https://example.com/")));
    }

    [Test]
    public void PageKeyForUrl_keepsAnExplicitNonDefaultPort()
    {
        Assert.That(Key("http://localhost:8081/connect"), Is.EqualTo("http://localhost:8081"));
    }
}
