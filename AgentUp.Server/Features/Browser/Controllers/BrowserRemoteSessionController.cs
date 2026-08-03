using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[ApiController]
[Route("api/browser/remote-session")]
public sealed class BrowserRemoteSessionController(BrowserRemoteSessionService remoteSessions) : ControllerBase
{
    [HttpGet("{workspaceId}")]
    public IActionResult Get(string workspaceId)
        => Ok(remoteSessions.GetSession(workspaceId));
}
