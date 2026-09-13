using AgentUp.Browser.Streaming.DTOs;
using AgentUp.Browser.Streaming.Resources;

namespace AgentUp.Browser.Streaming.Tests.Features.RemoteDisplay.Unit;

[TestFixture]
public sealed class RemoteDisplayViewerPageTests
{
    [Test]
    public void Builds_a_mobile_capable_viewer_with_encoded_configuration_and_bidirectional_input()
    {
        var html = RemoteDisplayViewerPage.Build(new RemoteDisplayViewerOptions(
            "App <name>", "/display?ticket=abc", "image/png", 1280, 800));

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("App &lt;name&gt;"));
            Assert.That(html, Does.Contain("\"socketPath\":\"/display?ticket=abc\""));
            Assert.That(html, Does.Contain("pointerdown"));
            Assert.That(html, Does.Contain("pointerup"));
            Assert.That(html, Does.Contain("keydown"));
            Assert.That(html, Does.Contain("visibilitychange"));
            Assert.That(html, Does.Not.Contain("setInterval"));
        });
    }
}
