using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Unit;

[TestFixture]
public sealed class ScreenshotCommandServiceTests
{
    [Test]
    public async Task Persist_capturesEachSceneAndCopiesHero()
    {
        var desktop = ScreenshotScene.Desktop();
        var mobile = ScreenshotScene.Mobile();
        var media = Store(desktop, mobile);
        var capture = new FakeWebScreenshotDriver();
        var result = await Service(media, capture, desktop, mobile)
            .PersistAsync(DebugDomain.Screenshots("persist").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(capture.Captures, Has.Count.EqualTo(2));
        Assert.That(capture.Captures[1].Width, Is.EqualTo(390));
        Assert.That(media.Copies[0].Destination, Is.EqualTo(media.HeroPath()));
        Assert.That(result.ArtifactPath, Is.EqualTo(media.HeroPath()));
    }

    [Test]
    public async Task Validate_checksContractThenPixels()
    {
        var scene = ScreenshotScene.Desktop();
        var media = Store(scene);
        var contract = new FakeScreenshotAppContract();
        var comparer = new FakeScreenshotPngComparer();
        var result = await Service(media, new FakeWebScreenshotDriver(), contract, comparer, scene)
            .ValidateAsync(DebugDomain.Screenshots("validate").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(contract.Verified, Is.EqualTo(new[] { scene.Id }));
        Assert.That(comparer.Compared, Has.Count.EqualTo(2));
        Assert.That(result.Message, Does.Contain("Validated 1"));
    }

    [Test]
    public async Task Validate_reportsPixelMismatch()
    {
        var scene = ScreenshotScene.Desktop();
        var comparer = new FakeScreenshotPngComparer { Match = false, Detail = "12 pixel(s) differ" };
        var result = await Service(Store(scene), new FakeWebScreenshotDriver(), new FakeScreenshotAppContract(), comparer, scene)
            .ValidateAsync(DebugDomain.Screenshots("validate").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("12 pixel(s) differ"));
    }

    [Test]
    public async Task Capture_filtersByView()
    {
        var git = ScreenshotScene.Desktop("git", hero: false);
        var apps = ScreenshotScene.Desktop();
        var capture = new FakeWebScreenshotDriver();
        var result = await Service(Store(git, apps), capture, git, apps)
            .CaptureAsync(DebugDomain.Screenshots("desktop").ForView("git").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(capture.Captures, Has.Count.EqualTo(1));
        Assert.That(capture.Captures[0].Url, Does.Contain("desktop-git.html"));
    }

    [Test]
    public async Task Capture_unknownView_fails()
    {
        var scene = ScreenshotScene.Desktop();
        var result = await Service(Store(scene), new FakeWebScreenshotDriver(), scene)
            .CaptureAsync(DebugDomain.Screenshots("desktop").ForView("metrics").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown screenshot view"));
    }

    [Test]
    public async Task ValidateLive_requiresHost()
    {
        var scene = ScreenshotScene.Mobile();
        var live = new FakeScreenshotLiveAppProbe();
        live.Pages[$"{DebugLayout.MobileUrl}{scene.LivePath}"] = "<p>hosted</p>";
        var sessions = new FakeSessionStore { Session = new HostSessionDto(1, "/tmp", "/tmp/session", []) };
        var result = await Service(Store(scene), new FakeWebScreenshotDriver(), new FakeScreenshotAppContract(), new FakeScreenshotPngComparer(), live, sessions, scene)
            .ValidateAsync(DebugDomain.Screenshots("validate").Live().Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("live apps match"));
    }

    [Test]
    public async Task Persist_timeout_returnsFailure()
    {
        var scene = ScreenshotScene.Desktop();
        var capture = new FakeWebScreenshotDriver { DelayUntilCanceled = true };
        var result = await Service(Store(scene), capture, scene)
            .PersistAsync(
                DebugDomain.Screenshots("persist").TimingOutAfter(TimeSpan.FromMilliseconds(30)).Build(),
                CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }

    [Test]
    public async Task Persist_emptyManifest_fails()
    {
        var result = await Service(new FakeScreenshotMediaStore(), new FakeWebScreenshotDriver())
            .PersistAsync(DebugDomain.Screenshots("persist").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Screenshot manifest has no scenes"));
    }

    [Test]
    public async Task Persist_missingHtml_fails()
    {
        var scene = ScreenshotScene.Desktop();
        var result = await Service(new FakeScreenshotMediaStore(), new FakeWebScreenshotDriver(), scene)
            .PersistAsync(DebugDomain.Screenshots("persist").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Screenshot HTML"));
    }

    [Test]
    public async Task ValidateLive_requiresRunningHost()
    {
        var scene = ScreenshotScene.Mobile();
        var probe = new FakeReadyProbe();
        probe.Ready[DebugLayout.MobileUrl] = false;
        var result = await Service(
                Store(scene),
                new FakeWebScreenshotDriver(),
                new FakeScreenshotAppContract(),
                new FakeScreenshotPngComparer(),
                new FakeScreenshotLiveAppProbe(),
                new FakeSessionStore(),
                probe,
                scene)
            .ValidateAsync(DebugDomain.Screenshots("validate").Live().Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("au-debug is not running"));
    }

    private static FakeScreenshotMediaStore Store(params AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto[] scenes)
    {
        var media = new FakeScreenshotMediaStore();
        foreach (var scene in scenes)
            media.Existing.Add(media.HtmlPath(scene));
        return media;
    }

    private static ScreenshotCommandService Service(
        FakeScreenshotMediaStore media,
        FakeWebScreenshotDriver capture,
        params AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto[] scenes)
        => Service(media, capture, new FakeScreenshotAppContract(), new FakeScreenshotPngComparer(), new FakeScreenshotLiveAppProbe(), new FakeSessionStore(), scenes);

    private static ScreenshotCommandService Service(
        FakeScreenshotMediaStore media,
        FakeWebScreenshotDriver capture,
        FakeScreenshotAppContract contract,
        FakeScreenshotPngComparer comparer,
        params AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto[] scenes)
        => Service(media, capture, contract, comparer, new FakeScreenshotLiveAppProbe(), new FakeSessionStore(), scenes);

    private static ScreenshotCommandService Service(
        FakeScreenshotMediaStore media,
        FakeWebScreenshotDriver capture,
        FakeScreenshotAppContract contract,
        FakeScreenshotPngComparer comparer,
        FakeScreenshotLiveAppProbe live,
        FakeSessionStore sessions,
        params AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto[] scenes)
        => Service(media, capture, contract, comparer, live, sessions, new FakeReadyProbe(), scenes);

    private static ScreenshotCommandService Service(
        FakeScreenshotMediaStore media,
        FakeWebScreenshotDriver capture,
        FakeScreenshotAppContract contract,
        FakeScreenshotPngComparer comparer,
        FakeScreenshotLiveAppProbe live,
        FakeSessionStore sessions,
        FakeReadyProbe probe,
        params AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto[] scenes)
    {
        var manifests = new FakeScreenshotManifestStore();
        manifests.Scenes.AddRange(scenes);
        return new ScreenshotCommandService(
            manifests,
            media,
            capture,
            contract,
            comparer,
            live,
            sessions,
            probe);
    }
}
