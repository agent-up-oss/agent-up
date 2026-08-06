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
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        // Await session creation (with a timeout) so the display loop is guaranteed to be
        // running before the subscriber registers. Fire-and-forget caused a race where
        // HasSubscribers was true but no loop had started yet, producing a blank screen.
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        sessionCts.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await sessions.EnsureSessionAsync(workspaceId, sessionCts.Token);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            // Timed out or session creation failed; proceed — viewer will retry.
        }
        await display.ConnectAsync(workspaceId, ws,
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
