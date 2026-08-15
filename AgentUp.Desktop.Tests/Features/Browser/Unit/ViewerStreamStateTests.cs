using AgentUp.Desktop.Features.Browser.Models;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class ViewerStreamStateTests
{
    private static StreamStateSnapshot Snap(
        StreamStateKind kind,
        string? chromiumState = null,
        int chromiumProgress = 0,
        int attempt = 0,
        int maxAttempts = 0,
        string? reason = null)
        => new("ws-1", kind, chromiumState, chromiumProgress, attempt, maxAttempts, reason, null);

    // ── WantsViewerWebView ────────────────────────────────────────────────────

    [Test]
    public void WantsWebView_whenStreaming_aiMode_activeTab_noTutorial()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.Streaming),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void DoesNotWantWebView_whenSnapIsNull()
        => Assert.That(MainWindow.WantsViewerWebView(null,
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    [Test]
    public void DoesNotWantWebView_whenNotAiMode()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.Streaming),
            isAi: false, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    [Test]
    public void DoesNotWantWebView_whenNotActiveTab()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.Streaming),
            isAi: true, isActiveViewerTab: false, tutorialVisible: false), Is.False);

    [Test]
    public void DoesNotWantWebView_whenTutorialVisible()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.Streaming),
            isAi: true, isActiveViewerTab: true, tutorialVisible: true), Is.False);

    [Test]
    public void DoesNotWantWebView_whenWorkspaceStopped()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.WorkspaceStopped),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    [Test]
    public void DoesNotWantWebView_whenSessionLaunching()
        => Assert.That(MainWindow.WantsViewerWebView(Snap(StreamStateKind.SessionLaunching),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    // ── ShouldDestroyViewerWebView ────────────────────────────────────────────

    [Test]
    public void ShouldDestroy_whenWorkspaceStopped_aiMode_activeTab()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.WorkspaceStopped),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void ShouldDestroy_whenSessionLaunching()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.SessionLaunching),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void ShouldDestroy_whenAppFailed()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.AppFailed),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void ShouldDestroy_whenAppConnecting()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.AppConnecting),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void ShouldDestroy_whenChromiumDownloading()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.ChromiumDownloading),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.True);

    [Test]
    public void ShouldNotDestroy_whenSnapIsNull()
    {
        // Null snap = no SSE received yet. Leave existing WebView alone so WebViews created
        // by HandleHeadlessNavigation aren't destroyed before NavigationCompleted can fire.
        Assert.That(MainWindow.ShouldDestroyViewerWebView(null,
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.False);
    }

    [Test]
    public void ShouldNotDestroy_whenStreaming()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.Streaming),
            isAi: true, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    [Test]
    public void ShouldNotDestroy_whenNotAiMode()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.WorkspaceStopped),
            isAi: false, isActiveViewerTab: true, tutorialVisible: false), Is.False);

    [Test]
    public void ShouldNotDestroy_whenNotActiveTab()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.WorkspaceStopped),
            isAi: true, isActiveViewerTab: false, tutorialVisible: false), Is.False);

    [Test]
    public void ShouldNotDestroy_whenTutorialVisible()
        => Assert.That(MainWindow.ShouldDestroyViewerWebView(Snap(StreamStateKind.WorkspaceStopped),
            isAi: true, isActiveViewerTab: true, tutorialVisible: true), Is.False);

    // ── Invariant: WantsWebView and ShouldDestroy are mutually exclusive ──────

    [Test]
    public void WantAndDestroyAreMutuallyExclusive_forAllKinds()
    {
        foreach (var kind in Enum.GetValues<StreamStateKind>())
        {
            var wants = MainWindow.WantsViewerWebView(Snap(kind), isAi: true, isActiveViewerTab: true, tutorialVisible: false);
            var destroys = MainWindow.ShouldDestroyViewerWebView(Snap(kind), isAi: true, isActiveViewerTab: true, tutorialVisible: false);
            Assert.That(wants && destroys, Is.False, $"Both WantsWebView and ShouldDestroy are true for kind={kind}");
        }
    }

    // ── ResolveBannerDecision ─────────────────────────────────────────────────

    [Test]
    public void Banner_connectingWithEllipsis_whenSnapIsNull()
    {
        var d = MainWindow.ResolveBannerDecision(null);
        Assert.That(d.ShowConnecting, Is.True);
        Assert.That(d.ConnectingText, Is.EqualTo("Connecting…"));
        Assert.That(d.ShowDownload, Is.False);
    }

    [Test]
    public void Banner_noBanners_whenStreaming()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.Streaming));
        Assert.That(d.ShowConnecting, Is.False);
        Assert.That(d.ShowDownload, Is.False);
    }

    [Test]
    public void Banner_workspaceStopped_showsConnecting()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.WorkspaceStopped));
        Assert.That(d.ShowConnecting, Is.True);
        Assert.That(d.ConnectingText, Is.EqualTo("Workspace stopped."));
        Assert.That(d.ShowDownload, Is.False);
    }

    [Test]
    public void Banner_sessionLaunching_showsConnecting()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.SessionLaunching));
        Assert.That(d.ShowConnecting, Is.True);
        Assert.That(d.ConnectingText, Is.EqualTo("Preparing browser session…"));
    }

    [Test]
    public void Banner_appFailed_showsReason()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.AppFailed, reason: "port unreachable"));
        Assert.That(d.ShowConnecting, Is.True);
        Assert.That(d.ConnectingText, Is.EqualTo("port unreachable"));
    }

    [Test]
    public void Banner_appFailed_fallsBackToDefaultMessage_whenNoReason()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.AppFailed));
        Assert.That(d.ConnectingText, Is.EqualTo("Could not reach app."));
    }

    [Test]
    public void Banner_appConnecting_includesAttemptCount_whenKnown()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.AppConnecting, attempt: 2, maxAttempts: 5));
        Assert.That(d.ConnectingText, Is.EqualTo("Connecting to app… (2 / 5)"));
    }

    [Test]
    public void Banner_appConnecting_omitsAttemptCount_whenNotYetKnown()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.AppConnecting));
        Assert.That(d.ConnectingText, Is.EqualTo("Connecting to app…"));
    }

    [Test]
    public void Banner_chromiumDownloading_showsProgressText()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.ChromiumDownloading, chromiumProgress: 42));
        Assert.That(d.ShowDownload, Is.True);
        Assert.That(d.ShowConnecting, Is.False);
        Assert.That(d.DownloadText, Is.EqualTo("Downloading Chromium… 42%"));
        Assert.That(d.DownloadFailed, Is.False);
        Assert.That(d.DownloadProgress, Is.EqualTo(42));
    }

    [Test]
    public void Banner_chromiumDownloading_isIndeterminate_whenProgressIsZero()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.ChromiumDownloading, chromiumProgress: 0));
        Assert.That(d.DownloadProgress, Is.EqualTo(0));
        Assert.That(d.DownloadFailed, Is.False);
        Assert.That(d.DownloadText, Is.EqualTo("Downloading Chromium…"));
    }

    [Test]
    public void Banner_chromiumDownloading_showsFailedText_whenChromiumStateFailed()
    {
        var d = MainWindow.ResolveBannerDecision(Snap(StreamStateKind.ChromiumDownloading, chromiumState: "failed"));
        Assert.That(d.DownloadFailed, Is.True);
        Assert.That(d.DownloadText, Is.EqualTo("Chromium download failed. AI mode unavailable."));
    }

    [Test]
    public void Banner_allKinds_neverHaveBothBannersVisible()
    {
        var allSnaps = Enum.GetValues<StreamStateKind>()
            .Select(k => Snap(k))
            .Append(null);

        foreach (var snap in allSnaps)
        {
            var d = MainWindow.ResolveBannerDecision(snap);
            Assert.That(d.ShowConnecting && d.ShowDownload, Is.False,
                $"Both banners visible for snap={(snap is null ? "null" : $"{snap.Kind}")}");
        }
    }
}
