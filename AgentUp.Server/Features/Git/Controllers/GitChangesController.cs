using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Git.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/git")]
public sealed class GitChangesController(GitChangeTreeService changes) : ControllerBase
{
    [HttpGet("changes")]
    public async Task<IActionResult> GetChanges(string workspaceId)
    {
        var tree = await changes.GetChangesAsync(workspaceId, HttpContext.RequestAborted);
        return FoundResult(this, tree);
    }

    [HttpGet("file")]
    public async Task<IActionResult> GetFileDiff(string workspaceId, [FromQuery] string path)
    {
        var diff = await changes.GetFileDiffAsync(workspaceId, path, HttpContext.RequestAborted);
        return FoundResult(this, diff);
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(string workspaceId, GitCommitRequest request)
    {
        var result = await changes.CommitAsync(workspaceId, request, HttpContext.RequestAborted);
        return CommitResult(this, result);
    }

    private static IActionResult FoundResult<T>(ControllerBase controller, T? value) where T : class
        => value is null ? controller.NotFound() : controller.Ok(value);

    private static IActionResult CommitResult(ControllerBase controller, GitCommitResult result)
    {
        if (!result.Found)
            return controller.NotFound();

        return result.Succeeded
            ? controller.Ok(result)
            : controller.Problem(detail: result.Error, statusCode: 400, title: "Commit failed");
    }
}
