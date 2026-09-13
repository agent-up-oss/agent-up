using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Features.ApplicationProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.ApplicationProxy.Controllers;

[ApiController]
[Route("api/apps/tickets")]
public sealed class ApplicationProxyTicketsController(ApplicationProxyService proxy) : ControllerBase
{
    [HttpPost]
    public IActionResult Issue(ApplicationProxyTicketRequest request)
        => TicketResult(this, proxy.IssueTicket(request));

    private static IActionResult TicketResult(ControllerBase controller, ApplicationProxyTicketIssueResult result)
    {
        if (result.Response is not null)
            return controller.Ok(result.Response);

        return controller.Problem(detail: result.Detail, statusCode: result.StatusCode, title: result.Title);
    }
}
