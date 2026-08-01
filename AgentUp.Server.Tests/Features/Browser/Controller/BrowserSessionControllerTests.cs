using AgentUp.Server.Features.Browser.Controllers;
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
}
