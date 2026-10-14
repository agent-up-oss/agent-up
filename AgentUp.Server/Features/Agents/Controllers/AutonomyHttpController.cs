using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

[ApiController]
[Route("api/autonomy")]
public sealed class AutonomyHttpController(AutonomousWorkspaceService autonomy) : ControllerBase
{
    [HttpGet]
    public ActionResult<ServerAutonomyDto> Get() =>
        Ok(new ServerAutonomyDto(autonomy.ServerEnabled, autonomy.HostConnectionPresent));

    [HttpPut]
    public IActionResult Put(ServerAutonomyRequest request) =>
        request.Enabled ? Enable() : Disable();

    private IActionResult Enable() =>
        autonomy.TryEnableServer(out var refusal)
            ? NoContent()
            : Problem(statusCode: 409, title: "Autonomy could not be enabled", detail: refusal);

    private IActionResult Disable()
    {
        autonomy.DisableServer();
        return NoContent();
    }
}
