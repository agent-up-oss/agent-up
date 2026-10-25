using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/review-snapshot")]
public sealed class WorkspaceReviewSnapshotHttpController(WorkspaceReviewSnapshotController snapshots) : ControllerBase
{
    [HttpGet]
    [Produces("text/html")]
    public async Task<IActionResult> Get(string workspaceId, CancellationToken cancellationToken)
    {
        var html = await snapshots.GetAsync(workspaceId, cancellationToken);
        if (html is null)
            return NotFound();

        Response.Headers.ContentDisposition = "attachment; filename=\"agent-up-report.html\"";
        return Content(html, "text/html; charset=utf-8");
    }
}
