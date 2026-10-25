using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Forge.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/forge")]
public sealed class ForgeOAuthHttpController(ForgeController forge) : ControllerBase
{
    [HttpGet("github/register")]
    public IActionResult GithubRegister([FromQuery] string state)
    {
        var html = forge.GithubRegistrationPage(state);
        return html is null ? NotFound() : Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("github/manifest/callback")]
    public async Task<IActionResult> GithubManifestCallback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        var installationUrl = await forge.CompleteGithubManifestAsync(code, state, cancellationToken);
        return installationUrl is null ? NotFound() : Redirect(installationUrl);
    }

    [HttpGet("github/install/callback")]
    public async Task<IActionResult> GithubInstallCallback(
        [FromQuery] string key,
        [FromQuery(Name = "installation_id")] long installationId,
        CancellationToken cancellationToken)
    {
        var completed = await forge.CompleteGithubInstallationAsync(key, installationId, cancellationToken);
        return completed
            ? Content("GitHub App installed. You can close this window and return to Agent-Up.", "text/plain; charset=utf-8")
            : NotFound();
    }

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
