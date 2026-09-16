using AgentUp.Server.Features.Commits.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Commits.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/commit-queue")]
public sealed class WorkspaceCommitQueueController(WorkspaceCommitQueueService queues) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string workspaceId)
    {
        var result = await queues.GetAsync(workspaceId, HttpContext.RequestAborted);
        return result.Found ? Ok(result.Queue) : NotFound();
    }
}
