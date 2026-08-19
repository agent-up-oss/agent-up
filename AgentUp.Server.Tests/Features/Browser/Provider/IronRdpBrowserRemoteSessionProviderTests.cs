using AgentUp.Browser.Streaming.Models;
using AgentUp.Server.Features.Browser.Providers;

namespace AgentUp.Server.Tests.Features.Browser.Provider;

[TestFixture]
public sealed class IronRdpBrowserRemoteSessionProviderTests
{
    [Test]
    public void GetSession_returns_ironrdp_metadata_with_standard_presets()
    {
        var provider = new IronRdpBrowserRemoteSessionProvider();

        var session = provider.GetSession("ws-1", BrowserControlMode.DefaultAi);

        Assert.Multiple(() =>
        {
            Assert.That(session.WorkspaceId, Is.EqualTo("ws-1"));
            Assert.That(session.Transport, Is.EqualTo("rdp"));
            Assert.That(session.ViewerPath, Is.EqualTo("/api/browser/rdp-viewer?workspaceId=ws-1"));
            Assert.That(session.DisplayWebSocketPath, Is.EqualTo("/api/browser/rdp/ws-1"));
            Assert.That(session.LatestFramePath, Is.EqualTo("/api/browser/rdp/ws-1/frame"));
            Assert.That(session.ControlAuthority, Is.EqualTo("ai"));
            Assert.That(session.SelectedPresetId, Is.EqualTo("desktop"));
            Assert.That(session.TouchCapable, Is.True);
            Assert.That(session.ViewportPresets.Select(p => p.Id),
                Is.EqualTo(["mobile", "tablet", "desktop", "wide", "full-hd"]));
        });
    }

    [Test]
    public void GetSession_reports_human_control_without_selecting_dynamic_dimensions_as_ai_preset()
    {
        var provider = new IronRdpBrowserRemoteSessionProvider();

        var session = provider.GetSession("ws-1", new BrowserControlMode(ControlAuthority.Human, 1437, 811));

        Assert.Multiple(() =>
        {
            Assert.That(session.ControlAuthority, Is.EqualTo("human"));
            Assert.That(session.Width, Is.EqualTo(1437));
            Assert.That(session.Height, Is.EqualTo(811));
            Assert.That(session.SelectedPresetId, Is.EqualTo("desktop"));
        });
    }
}
