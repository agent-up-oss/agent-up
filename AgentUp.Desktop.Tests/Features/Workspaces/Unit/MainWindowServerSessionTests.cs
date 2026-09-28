using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class MainWindowServerSessionTests
{
    [Test]
    public void NormalizeServerBaseUrl_trimsATrailingSlash()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                MainWindow.NormalizeServerBaseUrl(new Uri("http://127.0.0.1:5000/")),
                Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(MainWindow.NormalizeServerBaseUrl(null), Is.Null);
        });
    }

    [Test]
    public void ResolveSessionBaseUrl_prefersTheLiveConnectionUrl()
    {
        Assert.That(
            MainWindow.ResolveSessionBaseUrl("http://127.0.0.1:5100", "http://127.0.0.1:5000"),
            Is.EqualTo("http://127.0.0.1:5100"));
    }

    [Test]
    public void ResolveSessionBaseUrl_keepsFallbackWhenTheConnectionUrlIsMissing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MainWindow.ResolveSessionBaseUrl(null, "http://127.0.0.1:5000"), Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(MainWindow.ResolveSessionBaseUrl("  ", "http://127.0.0.1:5000"), Is.EqualTo("http://127.0.0.1:5000"));
        });
    }

    [Test]
    public void CreateServerScopedHttpBaseAddress_usesTheLiveServerOrigin()
    {
        Assert.That(
            MainWindow.CreateServerScopedHttpBaseAddress("http://127.0.0.1:5100"),
            Is.EqualTo(new Uri("http://127.0.0.1:5100")));
    }

    [Test]
    public void IsDemoConnection_usesTheLiveLoginUrlWhenHttpBaseAddressStayedOnTheRealServer()
    {
        var controller = FakeServerTestComposition.Controller();

        Assert.Multiple(() =>
        {
            Assert.That(
                MainWindow.IsDemoConnection(controller, "http://127.0.0.1:5000", FakeServerIdentity.Url),
                Is.True);
            Assert.That(
                MainWindow.IsDemoConnection(controller, "http://127.0.0.1:5000", "http://127.0.0.1:5000"),
                Is.False);
            Assert.That(MainWindow.IsDemoConnection(null, FakeServerIdentity.Url), Is.False);
        });
    }
}
