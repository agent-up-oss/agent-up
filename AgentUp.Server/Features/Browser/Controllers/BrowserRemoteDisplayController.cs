using System.Net.WebSockets;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserRemoteDisplayController(
    BrowserRemoteDisplayService display,
    BrowserInputDispatcher inputDispatcher,
    HeadlessBrowserSessionManager sessions) : ControllerBase
{
    [HttpGet("rdp/{workspaceId}")]
    public async Task StreamAsync(string workspaceId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest) { HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await sessions.StreamDisplayAsync(workspaceId, ws,
            json => inputDispatcher.DispatchAsync(workspaceId, json, HttpContext.RequestAborted),
            HttpContext.RequestAborted);
    }

    [HttpGet("rdp/{workspaceId}/frame")]
    public async Task<IActionResult> LatestFrame(string workspaceId)
    {
        var frame = await display.GetLatestFrameOrCaptureAsync(
            workspaceId,
            ct => sessions.CaptureDisplayFrameAsync(workspaceId, ct),
            HttpContext.RequestAborted);
        Response.Headers.CacheControl = "no-store";
        return frame is null ? NotFound() : File(frame, "image/jpeg");
    }

    [HttpGet("chromium-status")]
    public IActionResult ChromiumStatus()
    {
        var (state, progress) = sessions.GetChromiumStatus();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { state, progress });
    }
}
