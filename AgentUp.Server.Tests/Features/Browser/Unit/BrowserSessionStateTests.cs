using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserSessionStateTests
{
    [Test]
    public void PageKeyForUrl_groups_pages_by_origin()
    {
        var first = BrowserSessionState.PageKeyForUrl(new Uri("http://localhost:3000/dashboard"));
        var second = BrowserSessionState.PageKeyForUrl(new Uri("http://localhost:3000/settings"));

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void PageKeyForUrl_separates_application_ports()
    {
        var first = BrowserSessionState.PageKeyForUrl(new Uri("http://localhost:3000/dashboard"));
        var second = BrowserSessionState.PageKeyForUrl(new Uri("http://localhost:5000/swagger"));

        Assert.That(first, Is.Not.EqualTo(second));
    }
}
