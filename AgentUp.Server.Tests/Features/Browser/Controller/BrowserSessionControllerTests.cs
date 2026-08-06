using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserSessionControllerTests
{
    [Test]
    public void GetCurrentUrl_ReturnsNotFound_WhenNoActiveSession()
    {
        var result = MakeController().GetCurrentUrl("ws-1");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Navigate_ReturnsNoContent_WhenDispatchSucceeds()
    {
        var store = new BrowserSessionStore();
        var controller = new BrowserSessionController(store, new HeadlessBrowserSessionAccessor(null));

        var navigateTask = controller.Navigate("ws-1", "http://localhost:3000", true, CancellationToken.None);
        var command = await store.TryDequeueAsync(["ws-1"], TimeSpan.FromSeconds(1), CancellationToken.None);
        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, true, null, null));
        var result = await navigateTask;

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task Navigate_PreservesReloadIfSameUrlFlag()
    {
        var store = new BrowserSessionStore();
        var controller = new BrowserSessionController(store, new HeadlessBrowserSessionAccessor(null));

        var navigateTask = controller.Navigate("ws-1", "http://localhost:3000", false, CancellationToken.None);
        var command = await store.TryDequeueAsync(["ws-1"], TimeSpan.FromSeconds(1), CancellationToken.None);
        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, true, null, null));
        await navigateTask;

        Assert.That(command.ReloadIfSameUrl, Is.False);
    }

    [Test]
    public async Task Navigate_ReturnsBadRequest_WhenRequestIsCancelled()
    {
        var store = new BrowserSessionStore();
        var controller = new BrowserSessionController(store, new HeadlessBrowserSessionAccessor(null));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await controller.Navigate("ws-1", "http://localhost:3000", true, cts.Token);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Navigate_ReturnsBadRequest_WithDispatcherError_WhenDispatchFails()
    {
        var store = new BrowserSessionStore();
        var controller = new BrowserSessionController(store, new HeadlessBrowserSessionAccessor(null));

        var navigateTask = controller.Navigate("ws-1", "http://localhost:3000", true, CancellationToken.None);
        var command = await store.TryDequeueAsync(["ws-1"], TimeSpan.FromSeconds(1), CancellationToken.None);
        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, false, null, "Browser command execution failed."));
        var result = await navigateTask;

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Browser command execution failed."));
    }

    [Test]
    public async Task NavigateBack_ReturnsNotFound_WhenNoActiveSession()
    {
        var result = await MakeController().NavigateBack("ws-1", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task NavigateForward_ReturnsNotFound_WhenNoActiveSession()
    {
        var result = await MakeController().NavigateForward("ws-1", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Reload_ReturnsNotFound_WhenNoActiveSession()
    {
        var result = await MakeController().Reload("ws-1", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    private static BrowserSessionController MakeController()
        => new(new BrowserSessionStore(), new HeadlessBrowserSessionAccessor(null));
}
