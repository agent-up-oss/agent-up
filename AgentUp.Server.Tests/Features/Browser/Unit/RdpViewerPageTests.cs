using AgentUp.Server.Features.Browser.Resources;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class RdpViewerPageTests
{
    [Test]
    public void Build_starts_frame_polling_immediately()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.That(html, Does.Contain("startPolling();"));
        Assert.That(html, Does.Contain("pollFrame();"));
    }

    [Test]
    public void Build_coalesces_mousemove_and_does_not_emit_synthetic_clicks()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("requestAnimationFrame(flushMove)"));
            Assert.That(html, Does.Not.Contain("send({ type: 'click'"));
        });
    }

    [Test]
    public void Build_renders_remote_cursor_kinds_from_control_messages()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("m.type === 'cursor'"));
            Assert.That(html, Does.Contain("cursor.classList.add('pointer')"));
            Assert.That(html, Does.Contain("cursor.classList.add('text')"));
        });
    }
}
