using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Applications.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class ApplicationMetricsHttpController(
    ApplicationMetricsService metrics,
    WorkspaceQueryController workspaces) : ControllerBase
{
    [HttpGet("{id}/applications/{name}/metrics")]
    public async Task<IActionResult> GetMetrics(
        string id,
        string name,
        int limit = 60,
        CancellationToken cancellationToken = default)
    {
        if (workspaces.GetById(id) is null)
            return NotFound();

        var timeline = await metrics.GetTimelineAsync(id, name, limit, cancellationToken);
        return Ok(timeline);
    }
}
