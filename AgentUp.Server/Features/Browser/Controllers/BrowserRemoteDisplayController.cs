using System.Net.WebSockets;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserRemoteDisplayController(
    BrowserRemoteDisplayService display,
    BrowserInputDispatcher inputDispatcher) : ControllerBase
{
    [HttpGet("rdp/{workspaceId}")]
    public async Task StreamAsync(string workspaceId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await display.ConnectAsync(workspaceId, ws,
            json => inputDispatcher.DispatchAsync(workspaceId, json, HttpContext.RequestAborted),
            HttpContext.RequestAborted);
    }

    [HttpGet("rdp/{workspaceId}/frame")]
    public IActionResult LatestFrame(string workspaceId)
    {
        display.RegisterPollingViewer(workspaceId);
        if (!display.TryGetLatestFrame(workspaceId, out var frame))
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return File(frame, "image/jpeg");
    }
}
