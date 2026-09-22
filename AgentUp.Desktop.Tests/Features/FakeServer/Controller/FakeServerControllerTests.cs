using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Controller;

[TestFixture]
public sealed class FakeServerControllerTests
{
    [Test]
    public void Catalog_describesTheBuiltInDemoServer()
    {
        var controller = FakeServerTestComposition.Controller();
        var catalog = controller.Catalog(FakeServerIdentity.Id);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Id, Is.EqualTo("fake"));
            Assert.That(catalog.Url, Is.EqualTo("http://127.0.0.1:9"));
            Assert.That(catalog.DisplayName, Is.EqualTo("Demo"));
            Assert.That(catalog.IsActive, Is.True);
        });
    }

    [Test]
    public void Matches_recognizesTheSentinelUrl()
    {
        var controller = FakeServerTestComposition.Controller();

        Assert.Multiple(() =>
        {
            Assert.That(controller.Matches("http://127.0.0.1:9"), Is.True);
            Assert.That(controller.Matches(new Uri("http://127.0.0.1:9/api/workspaces")), Is.True);
            Assert.That(controller.Matches("http://127.0.0.1:5000"), Is.False);
        });
    }

    [Test]
    public void ApplicationHtml_returnsTheBundledStorefront()
    {
        var controller = FakeServerTestComposition.Controller();

        Assert.That(controller.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        Assert.That(controller.ApplicationHtml(1), Is.Null);
    }

    [Test]
    public void Reset_restoresBundledWorkspaces()
    {
        var backend = FakeServerTestComposition.Backend();
        var controller = FakeServerTestComposition.Controller(backend);
        backend.Handle(new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop").Build());

        controller.Reset();

        Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Mug"));
    }
}
