using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Services;
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
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var controller = CreateController(display);

        controller.LatestFrame("workspace");

        Assert.That(display.HasSubscribers("workspace"), Is.True);
    }

    [Test]
    public void Input_activity_marks_workspace_active()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);

        display.RegisterInputActivity("workspace");

        Assert.That(display.HasActiveInput("workspace"), Is.True);
    }

    private static BrowserRemoteDisplayController CreateController(
        BrowserRemoteDisplayService? display = null)
    {
        var controller = new BrowserRemoteDisplayController(
            display ?? new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            inputDispatcher: null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }
}
