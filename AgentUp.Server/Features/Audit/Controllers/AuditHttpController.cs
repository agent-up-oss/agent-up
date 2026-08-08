using AgentUp.Server.Features.Audit.Models;
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
}
