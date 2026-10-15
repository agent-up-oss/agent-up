using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Forge.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/forge")]
public sealed class ForgeOAuthHttpController(ForgeController forge) : ControllerBase
{
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        var completed = await forge.CompleteLoginAsync(code, state, cancellationToken);
        return completed ? Ok(new { signedIn = true }) : NotFound();
    }
}
