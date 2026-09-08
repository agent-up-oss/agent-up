using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.SourceClones.Controllers;

[ApiController]
[Route("api/source-clones")]
public sealed class SourceClonesController(SourceCloneService clones) : ControllerBase
{
    [HttpGet("root")]
    public ActionResult<SourceCloneRoot> GetRoot() => clones.GetRoot();

    [HttpPost]
    public async Task<IActionResult> Clone(CloneSourceRequest request)
    {
        var result = await clones.CloneAsync(request, HttpContext.RequestAborted);
        return CloneResult(this, result);
    }

    private static IActionResult CloneResult(ControllerBase controller, SourceCloneResult result)
        => result is { Succeeded: true, Workspace: { } workspace }
            ? controller.Created($"/api/workspaces/{workspace.Id}", workspace)
            : controller.Problem(detail: result.Error, statusCode: 400, title: "Source clone failed");
}
