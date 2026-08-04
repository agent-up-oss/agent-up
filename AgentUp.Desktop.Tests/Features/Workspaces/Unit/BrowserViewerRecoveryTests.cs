using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class BrowserViewerRecoveryTests
{
    [Test]
    public void ViewerWebView_reclaims_internal_error_page()
    {
        var shouldReclaim = MainWindow.ShouldReclaimViewerUrl(
            "chrome-error://chromewebdata/",
            "http://localhost:5001/api/browser/rdp-viewer?workspaceId=ws-1");

        Assert.That(shouldReclaim, Is.True);
    }

    [Test]
    public void ViewerWebView_keeps_active_viewer_page()
    {
        var shouldReclaim = MainWindow.ShouldReclaimViewerUrl(
            "http://localhost:5001/api/browser/rdp-viewer?workspaceId=ws-1",
            "http://localhost:5001/api/browser/rdp-viewer?workspaceId=ws-1");

        Assert.That(shouldReclaim, Is.False);
    }
}
