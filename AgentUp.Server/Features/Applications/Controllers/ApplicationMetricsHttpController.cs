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
        var workspace = workspaces.GetById(id);
        if (workspace is null)
            return NotFound();

        if (!workspace.Applications.Any(app => string.Equals(app.Name, name, StringComparison.Ordinal)))
            return NotFound();

        var timeline = await metrics.GetTimelineAsync(id, name, limit, cancellationToken);
        return Ok(timeline);
    }
}
