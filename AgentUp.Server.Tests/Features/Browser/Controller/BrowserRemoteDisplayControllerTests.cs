using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserRemoteDisplayControllerTests
{
    [Test]
    public async Task LatestFrame_returns_no_store_jpeg_when_frame_exists()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        await display.BroadcastFrameAsync("workspace", [1, 2, 3], CancellationToken.None);
        var controller = CreateController(display);

        var result = await controller.LatestFrame("workspace");

        Assert.That(result, Is.InstanceOf<FileContentResult>());
        var file = (FileContentResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(file.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(file.FileContents, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        });
    }

    [Test]
    public async Task LatestFrame_returns_not_found_before_first_frame()
    {
        var controller = CreateController();

        var result = await controller.LatestFrame("workspace");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task LatestFrame_registers_polling_viewer_interest()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var controller = CreateController(display);

        await controller.LatestFrame("workspace");

        Assert.That(display.HasSubscribers("workspace"), Is.True);
    }

    [Test]
    public void Input_activity_marks_workspace_active()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);

        display.RegisterInputActivity("workspace");

        Assert.That(display.HasActiveInput("workspace"), Is.True);
    }

    [Test]
    public async Task GetLatestFrameOrCaptureAsync_captures_when_no_cached_frame_exists()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);

        var frame = await display.GetLatestFrameOrCaptureAsync(
            "workspace",
            _ => Task.FromResult<byte[]?>([4, 5, 6]),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(frame, Is.EqualTo(new byte[] { 4, 5, 6 }));
            Assert.That(display.HasSubscribers("workspace"), Is.True);
        });
    }

    private static BrowserRemoteDisplayController CreateController(
        BrowserRemoteDisplayService? display = null)
    {
        var controller = new BrowserRemoteDisplayController(
            display ?? new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            inputDispatcher: null!,
            sessions: CreateSessionManager(display));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static HeadlessBrowserSessionManager CreateSessionManager(BrowserRemoteDisplayService? display)
        => new(
            "/unused/chromium",
            "/unused/browser-profiles",
            display ?? new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            ServerTestComposition.CreateStreamState(),
            NullLogger<HeadlessBrowserSessionManager>.Instance);
}
