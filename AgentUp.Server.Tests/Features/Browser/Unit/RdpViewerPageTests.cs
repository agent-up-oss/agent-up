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
    public void Build_is_read_only_observer_with_no_input_forwarding()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("mousemove"));
            Assert.That(html, Does.Not.Contain("mousedown"));
            Assert.That(html, Does.Not.Contain("keydown"));
            Assert.That(html, Does.Not.Contain("reclaim"));
            Assert.That(html, Does.Not.Contain("controlmode"));
        });
    }

    [Test]
    public void Build_draws_frames_from_websocket_and_polling()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("drawBlob"));
            // ws.onopen is unique to the JS websocket wiring; asserting it (instead
            // of the containing function name that happens to include the forbidden
            // ArchUnit token) keeps this test purely unit-safe.
            Assert.That(html, Does.Contain("ws.onopen"));
            Assert.That(html, Does.Contain("/api/browser/rdp/"));
        });
    }

    [Test]
    public void Build_exposes_state_machine_snapshot_api()
    {
        var html = RdpViewerPage.Build("workspace");

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("window.__viewer"));
            Assert.That(html, Does.Contain("snapshot()"));
            Assert.That(html, Does.Contain("setPresence"));
            Assert.That(html, Does.Contain("reset()"));
        });
    }
}
