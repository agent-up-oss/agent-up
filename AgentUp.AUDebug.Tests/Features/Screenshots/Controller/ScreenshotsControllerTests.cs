using AgentUp.AUDebug.Features.Screenshots.Controllers;
using AgentUp.AUDebug.Features.Screenshots.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Controller;

[TestFixture]
public sealed class ScreenshotsControllerTests
{
    [Test]
    public async Task Persist_writesMediaHero()
    {
        var media = new FakeScreenshotMediaStore();
        var capture = new FakeWebScreenshotDriver();
        var scene = ScreenshotScene.Desktop();
        media.Existing.Add(media.HtmlPath(scene));
        var result = await Controller(media, capture, [scene]).RunAsync(
            DebugDomain.Screenshots("persist").Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(media.Copies, Has.Count.EqualTo(1));
        Assert.That(media.Copies[0].Destination, Does.Contain("screenshot.png"));
        Assert.That(capture.Captures[0].Width, Is.EqualTo(1440));
    }

    [Test]
    public async Task UnknownAction_returnsError()
    {
        var result = await Controller(new FakeScreenshotMediaStore(), new FakeWebScreenshotDriver(), [])
            .RunAsync(DebugDomain.Screenshots("explode").Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown screenshots action"));
    }

    private static ScreenshotsController Controller(
        FakeScreenshotMediaStore media,
        FakeWebScreenshotDriver capture,
        IReadOnlyList<AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto> scenes)
    {
        var manifests = new FakeScreenshotManifestStore();
        manifests.Scenes.AddRange(scenes);
        return new ScreenshotsController(
            new ScreenshotCommandService(
                manifests,
                media,
                capture,
                new FakeScreenshotAppContract(),
                new FakeScreenshotPngComparer(),
                new FakeScreenshotLiveAppProbe(),
                new FakeSessionStore(),
                new FakeReadyProbe()));
    }
}
