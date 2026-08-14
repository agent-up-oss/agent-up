using AgentUp.Browser.Streaming.Resources;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserViewerController : ControllerBase
{
    [HttpGet("rdp-viewer")]
    public IActionResult Viewer([FromQuery] string workspaceId)
    {
        if (string.IsNullOrEmpty(workspaceId))
            return BadRequest("workspaceId is required.");
        // Cache-Control: no-store prevents WebKit's back-forward cache from freezing
        // this page instead of destroying it on reload. Without it, each ReloadWebView
        // leaves the previous page's JS context alive (setInterval + WebSocket + all
        // closure state) — accumulating zombie viewers that hold subscriber slots on
        // the server and fire ghost heartbeats when their BFCache page is thawed.
        Response.Headers.CacheControl = "no-store";
        return Content(RdpViewerPage.Build(workspaceId), "text/html");
    }

    [HttpGet("mode")]
    public ContentResult Mode() => Content("headless", "text/plain");
}
