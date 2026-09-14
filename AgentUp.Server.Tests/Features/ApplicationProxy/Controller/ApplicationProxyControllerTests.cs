using AgentUp.Server.Features.ApplicationProxy.Controllers;
using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.ApplicationProxy.Controller;

[TestFixture]
public sealed class ApplicationProxyTicketsControllerTests
{
    [Test]
    public async Task Issue_returnsTheTicketForAnOpenHttpPort()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var controller = new ApplicationProxyTicketsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = ApplicationProxyHarness.LoopbackContext() }
        };

        var result = controller.Issue(new ApplicationProxyTicketRequest(workspaceId, port));

        var body = (ApplicationProxyTicketResponse)((OkObjectResult)result).Value!;
        Assert.That(body.BootstrapPath, Is.EqualTo($"/apps/{workspaceId}/{port}"));
        Assert.That(body.Ticket, Is.Not.Empty);
    }

    [Test]
    public async Task Issue_returnsAProblemWhenThePortIsClosed()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync(portOpen: false);
        var controller = new ApplicationProxyTicketsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = ApplicationProxyHarness.LoopbackContext() }
        };

        var result = controller.Issue(new ApplicationProxyTicketRequest(workspaceId, port));

        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
    }
}

[TestFixture]
public sealed class ApplicationProxyControllerTests
{
    [Test]
    public async Task OpenRoot_redirectsAValidTicketToTheOriginRoot()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var controller = new ApplicationProxyController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = ApplicationProxyHarness.LoopbackContext() }
        };
        controller.HttpContext.Request.Method = HttpMethods.Get;
        controller.HttpContext.Request.Headers[ApplicationProxyConstants.TicketHeader] = ticket;

        await controller.OpenRoot(workspaceId, port);

        Assert.That(controller.HttpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(controller.HttpContext.Response.Headers.Location.ToString(), Is.EqualTo("/"));
    }

    [Test]
    public async Task Open_redirectsAValidTicketToTheOriginRoot()
    {
        var (service, workspaceId, port, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var ticket = service.IssueTicket(new ApplicationProxyTicketRequest(workspaceId, port), ApplicationProxyHarness.LoopbackContext()).Response!.Ticket;
        var controller = new ApplicationProxyController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = ApplicationProxyHarness.LoopbackContext() }
        };
        controller.HttpContext.Request.Method = HttpMethods.Get;
        controller.HttpContext.Request.Headers[ApplicationProxyConstants.TicketHeader] = ticket;

        await controller.Open(workspaceId, port, "login");

        Assert.That(controller.HttpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
        Assert.That(controller.HttpContext.Response.Headers.Location.ToString(), Is.EqualTo("/"));
    }

    [Test]
    public async Task ForwardFallback_returnsNotFoundWithoutASessionCookie()
    {
        var (service, _, _, _, _, _, _) = await ApplicationProxyHarness.CreateAsync();
        var controller = new ApplicationProxyFallbackController(service);
        var context = ApplicationProxyHarness.LoopbackContext();

        await controller.ForwardFallback(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
