using AgentUp.Browser.Streaming.Models;
using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[ApiController]
[Route("api/browser")]
public sealed class BrowserSessionController(BrowserSessionStore store, HeadlessBrowserSessionAccessor sessions) : ControllerBase
{
    [HttpGet("current-url/{workspaceId}")]
    public IActionResult GetCurrentUrl(string workspaceId)
    {
        var session = sessions.GetSession(workspaceId);
        return session is null ? NotFound() : Content(session.CurrentUrl, "text/plain");
    }

    [HttpPost("navigate/{workspaceId}")]
    public async Task<IActionResult> Navigate(
        string workspaceId,
        [FromQuery] string url,
        [FromQuery] bool? reloadIfSameUrl,
        CancellationToken ct)
    {
        var command = new BrowserCommandDto(Guid.NewGuid(), workspaceId, BrowserCommandKind.Navigate, url, null, null, null, 15000, reloadIfSameUrl ?? true);
        var result = await store.DispatchAsync(command, TimeSpan.FromSeconds(15), ct);
        return result.Success ? NoContent() : BadRequest(result.Error);
    }

    [HttpPost("navigate-back/{workspaceId}")]
    public async Task<IActionResult> NavigateBack(string workspaceId, CancellationToken ct)
    {
        var session = sessions.GetSession(workspaceId);
        if (session is null) return NotFound();
        await session.GoBackAsync(ct);
        return NoContent();
    }

    [HttpPost("navigate-forward/{workspaceId}")]
    public async Task<IActionResult> NavigateForward(string workspaceId, CancellationToken ct)
    {
        var session = sessions.GetSession(workspaceId);
        if (session is null) return NotFound();
        await session.GoForwardAsync(ct);
        return NoContent();
    }

    [HttpPost("reload/{workspaceId}")]
    public async Task<IActionResult> Reload(string workspaceId, CancellationToken ct)
    {
        var session = sessions.GetSession(workspaceId);
        if (session is null) return NotFound();
        await session.ReloadAsync(ct);
        return NoContent();
    }
}
