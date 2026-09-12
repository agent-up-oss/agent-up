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

    [HttpGet("head")]
    public async Task<IActionResult> GetHead(string workspaceId)
    {
        var head = await changes.GetHeadAsync(workspaceId, HttpContext.RequestAborted);
        return FoundResult(this, head);
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

    [HttpPost("discard")]
    public async Task<IActionResult> Discard(string workspaceId, GitFilesRequest request)
    {
        var result = await changes.DiscardAsync(workspaceId, request, HttpContext.RequestAborted);
        return MutationResult(this, result, "Discard failed");
    }

    [HttpPost("branch")]
    public async Task<IActionResult> SwitchBranch(string workspaceId, GitBranchRequest request)
    {
        var result = await changes.SwitchBranchAsync(workspaceId, request, HttpContext.RequestAborted);
        return MutationResult(this, result, "Branch switch failed");
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

    private static IActionResult MutationResult(ControllerBase controller, GitMutationResult result, string title)
    {
        if (!result.Found)
            return controller.NotFound();

        return result.Succeeded
            ? controller.Ok(result)
            : controller.Problem(detail: result.Error, statusCode: 400, title: title);
    }
}
