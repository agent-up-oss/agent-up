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

    /// <summary>
    /// The bytes behind one artifact, for a surface that shows it rather than an agent that
    /// reads it. A product decision points at its evidence this way, so the screenshot lives in
    /// the audit store once instead of being copied into every feature that refers to it.
    /// </summary>
    [HttpGet("artifacts/{artifactId}")]
    public async Task<IActionResult> Artifact(string artifactId, CancellationToken ct)
    {
        var artifact = await audit.LoadArtifactAsync(artifactId, includeImage: true, ct);
        if (artifact?.ImageBase64 is null)
            return NotFound();

        return File(Convert.FromBase64String(artifact.ImageBase64), artifact.MimeType);
    }

    [HttpGet("workspaces/{workspaceId}/applications/{application}")]
    public async Task<ActionResult<DTOs.AuditEventPageDto>> QueryApplication(
        string workspaceId,
        string application,
        [FromQuery] string[]? kinds,
        [FromQuery] string[]? streams,
        [FromQuery] DateTimeOffset? before,
        [FromQuery] string? beforeEventId,
        [FromQuery, Range(1, 100)] int limit = 50,
        CancellationToken ct = default)
    {
        var query = new DTOs.AuditEventQuery(
            workspaceId, null, null, null, null, null, null, null, null, null, limit, null, application, before, beforeEventId, kinds, streams);
        return Ok(await audit.QueryPageAsync(query, ct));
    }
}
