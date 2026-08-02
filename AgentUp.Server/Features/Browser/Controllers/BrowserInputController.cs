using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[Route("api/browser/input")]
public sealed class BrowserInputController(HeadlessBrowserSessionManager manager) : ControllerBase
{
    [HttpGet("control-mode/{workspaceId}")]
    public IActionResult GetControlMode(string workspaceId)
        => Ok(manager.GetControlModeDto(workspaceId));

    [HttpPost("control-mode/{workspaceId}")]
    public async Task<IActionResult> SetControlModeAsync(
        string workspaceId,
        [FromQuery] string authority,
        [FromQuery] int width = 1280,
        [FromQuery] int height = 720,
        CancellationToken ct = default)
    {
        var (success, error) = await manager.TrySetControlModeAsync(workspaceId, authority, width, height, ct);
        return success ? Ok(manager.GetControlModeDto(workspaceId)) : BadRequest(error);
    }

    [HttpPost("viewport/{workspaceId}")]
    public async Task<IActionResult> SetViewportAsync(
        string workspaceId,
        [FromQuery] int width,
        [FromQuery] int height,
        CancellationToken ct = default)
    {
        var success = await manager.TrySetViewportAsync(workspaceId, width, height, ct);
        return success ? NoContent() : Conflict("Viewport update rejected: workspace is in AI mode.");
    }
}
