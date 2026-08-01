using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserSessionControllerTests
{
    [Test]
    public async Task GetPendingCommand_ReturnsBadRequest_WhenWorkspaceIdsAreMissing()
    {
        var store = new BrowserSessionStore();
        var controller = new BrowserSessionController(
            store,
            new BrowserPendingCommandService(store, new BrowserWorkspaceIdParser()));

        var result = await controller.GetPendingCommand(null);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task GetPendingCommand_ReturnsNoContent_WhenNoCommandIsPending()
    {
        var controller = Controller();

        var result = await controller.GetPendingCommand("workspace", timeoutMs: 1);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task GetPendingCommand_ReturnsOk_WhenCommandIsPending()
    {
        var store = new BrowserSessionStore();
        var controller = Controller(store);
        var command = Command(Guid.NewGuid());
        var dispatch = store.DispatchAsync(command, TimeSpan.FromSeconds(1), CancellationToken.None);

        var result = await controller.GetPendingCommand("workspace", timeoutMs: 1000);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(((OkObjectResult)result).Value, Is.EqualTo(command));

        store.CompleteCommand(new BrowserCommandResultDto(command.CommandId, true, null, null));
        await dispatch;
    }

    [Test]
    public async Task PostCommandResult_CompletesPendingCommand()
    {
        var store = new BrowserSessionStore();
        var controller = Controller(store);
        var command = Command(Guid.NewGuid());
        var dispatch = store.DispatchAsync(command, TimeSpan.FromSeconds(1), CancellationToken.None);

        await store.TryDequeueAsync(["workspace"], TimeSpan.FromSeconds(1), CancellationToken.None);
        var result = controller.PostCommandResult(new BrowserCommandResultDto(command.CommandId, true, "{}", null));
        var completed = await dispatch;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(completed.Success, Is.True);
            Assert.That(completed.Data, Is.EqualTo("{}"));
        });
    }

    private static BrowserSessionController Controller(BrowserSessionStore? store = null)
    {
        store ??= new BrowserSessionStore();
        return new BrowserSessionController(
            store,
            new BrowserPendingCommandService(store, new BrowserWorkspaceIdParser()));
    }

    private static BrowserCommandDto Command(Guid id) =>
        new(id, "workspace", BrowserCommandKind.Click, null, "#save", null, null, 100);
}
