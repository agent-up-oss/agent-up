using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[ApiController]
[Route("api/browser")]
public sealed class BrowserSessionController(BrowserSessionStore store) : ControllerBase
{
    [HttpGet("pending-command")]
    public async Task<IActionResult> GetPendingCommand(
        [FromQuery] string? workspaceIds,
        [FromQuery] int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        var ids = workspaceIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];

        if (ids.Count == 0)
            return BadRequest("workspaceIds query parameter is required.");

        var command = await store.TryDequeueAsync(ids, TimeSpan.FromMilliseconds(timeoutMs), ct);
        return command is null ? NoContent() : Ok(command);
    }

    [HttpPost("command-result")]
    public IActionResult PostCommandResult([FromBody] BrowserCommandResultDto result)
    {
        store.CompleteCommand(result);
        return NoContent();
    }
}
