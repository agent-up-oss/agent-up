using System.ComponentModel.DataAnnotations;
using AgentUp.Server.Features.Diagnostics.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

[ApiController]
[Route("api/diagnostics/workspaces")]
public sealed class WorkspaceDiagnosticsHttpController(WorkspaceDiagnosticsController diagnostics) : ControllerBase
{
    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<WorkspaceDiagnosticsDto>> Get(
        string workspaceId,
        [FromQuery] string? application,
        [FromQuery, Range(1, 1000)] int logLimit = 200,
        [FromQuery, Range(1, 500)] int entryLimit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await diagnostics.GetAsync(workspaceId, application, logLimit, entryLimit, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
