using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.TaskSource.Controllers;

/// <summary>
/// Where the repository host sends the reader back to.
///
/// The host calls this, not the client, so it is anonymous: the browser following the redirect
/// carries no Agent-Up credential. What makes it safe is the state, which was issued when the
/// sign-in began and is spent the first time it is used.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/task-source")]
public sealed class TaskSourceOAuthHttpController(TaskSourceController source) : ControllerBase
{
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        var connected = await source.CompleteLoginAsync(code, state, cancellationToken);
        return connected
            ? Content("Task source connected. You can close this window and return to Agent-Up.", "text/plain; charset=utf-8")
            : NotFound();
    }
}
