using System.Net.WebSockets;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser")]
public sealed class BrowserScreencastController(
    ScreencastBroadcastService broadcast,
    BrowserInputDispatcher inputDispatcher) : ControllerBase
{
    [HttpGet("screencast/{workspaceId}")]
    public async Task StreamAsync(string workspaceId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await broadcast.ConnectAsync(workspaceId, ws,
            json => inputDispatcher.DispatchAsync(workspaceId, json, HttpContext.RequestAborted),
            HttpContext.RequestAborted);
    }
}
