using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Browser.Controllers;

[ApiController]
[Route("api/browser")]
public sealed class BrowserSessionController(
    BrowserSessionStore store,
    BrowserPendingCommandService pendingCommands) : ControllerBase
{
    [HttpGet("pending-command")]
    public async Task<IActionResult> GetPendingCommand(
        [FromQuery] string? workspaceIds,
        [FromQuery] int timeoutMs = 5000,
        CancellationToken ct = default)
        => PendingCommandResult(await pendingCommands.GetAsync(workspaceIds, timeoutMs, ct));

    [HttpPost("command-result")]
    public IActionResult PostCommandResult([FromBody] BrowserCommandResultDto result)
    {
        store.CompleteCommand(result);
        return NoContent();
    }

    private IActionResult PendingCommandResult(BrowserPendingCommandResult result)
        => result.HasWorkspaceIds
            ? ExistingWorkspaceCommandResult(result)
            : BadRequest("workspaceIds query parameter is required.");

    private IActionResult ExistingWorkspaceCommandResult(BrowserPendingCommandResult result)
        => result.Command is null ? NoContent() : Ok(result.Command);
}
