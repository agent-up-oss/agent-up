using AgentUp.Server.Features.Audit.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Audit.Controllers;

[ApiController]
[Route("api/audit")]
public sealed class AuditHttpController(AuditController audit) : ControllerBase
{
    // Accepts audit events from external local clients (e.g. the desktop app).
    // The server enriches the event with workspace git identity if workspaceId is set.
    [HttpPost("record")]
    public async Task<IActionResult> Record(
        [FromBody] AuditRecordRequest request,
        CancellationToken ct)
    {
        await audit.RecordAsync(request, ct);
        return NoContent();
    }

    [HttpGet("workspaces/{workspaceId}/applications/{application}")]
    public async Task<ActionResult<DTOs.AuditEventPageDto>> QueryApplication(
        string workspaceId,
        string application,
        [FromQuery] DateTimeOffset? before,
        [FromQuery, Range(1, 100)] int limit = 50,
        CancellationToken ct = default)
    {
        var query = new DTOs.AuditEventQuery(
            workspaceId, null, null, null, null, "frontend", null, null, null, null, limit, application, before);
        return Ok(await audit.QueryPageAsync(query, ct));
    }
}
