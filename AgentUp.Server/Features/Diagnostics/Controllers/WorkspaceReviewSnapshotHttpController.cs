using AgentUp.Server.Features.Diagnostics.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/review-snapshot")]
public sealed class WorkspaceReviewSnapshotHttpController(WorkspaceReviewSnapshotController snapshots) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WorkspaceReviewSnapshotDto>> Get(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.GetAsync(workspaceId, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}
