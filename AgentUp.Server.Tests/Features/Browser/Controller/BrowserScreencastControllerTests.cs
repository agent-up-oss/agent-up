using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserScreencastControllerTests
{
    [Test]
    public async Task LatestFrame_returns_no_store_jpeg_when_frame_exists()
    {
        var broadcast = new ScreencastBroadcastService(NullLogger<ScreencastBroadcastService>.Instance);
        await broadcast.BroadcastFrameAsync("workspace", [1, 2, 3], CancellationToken.None);
        var controller = CreateController(broadcast);

        var result = controller.LatestFrame("workspace");

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
    public void LatestFrame_returns_not_found_before_first_frame()
    {
        var controller = CreateController();

        var result = controller.LatestFrame("workspace");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public void LatestFrame_registers_polling_viewer_interest()
    {
        var broadcast = new ScreencastBroadcastService(NullLogger<ScreencastBroadcastService>.Instance);
        var controller = CreateController(broadcast);

        controller.LatestFrame("workspace");

        Assert.That(broadcast.HasSubscribers("workspace"), Is.True);
    }

    private static BrowserScreencastController CreateController(
        ScreencastBroadcastService? broadcast = null)
    {
        var controller = new BrowserScreencastController(
            broadcast ?? new ScreencastBroadcastService(NullLogger<ScreencastBroadcastService>.Instance),
            inputDispatcher: null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}
