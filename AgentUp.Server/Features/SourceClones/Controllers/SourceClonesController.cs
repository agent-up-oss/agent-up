using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpPost("{workspaceId}/refresh")]
    public async Task<IActionResult> Refresh(
        string workspaceId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshSourceRequest? request)
    {
        var result = await clones.RefreshAsync(workspaceId, request?.Revision, HttpContext.RequestAborted);
        return result is { Succeeded: true, Workspace: { } workspace }
            ? Ok(workspace)
            : Problem(detail: result.Error, statusCode: 400, title: "Source clone refresh failed");
    }

    private static IActionResult CloneResult(ControllerBase controller, SourceCloneResult result)
        => result is { Succeeded: true, Workspace: { } workspace }
            ? controller.Created($"/api/workspaces/{workspace.Id}", workspace)
            : controller.Problem(detail: result.Error, statusCode: 400, title: "Source clone failed");
}
