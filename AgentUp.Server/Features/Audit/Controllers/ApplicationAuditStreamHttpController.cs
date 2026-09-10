using AgentUp.Server.Features.Audit.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Audit.Controllers;

[ApiController]
[Route("api/audit/workspaces/{workspaceId}/applications/{application}")]
public sealed class ApplicationAuditStreamHttpController(ApplicationAuditStreamService stream) : ControllerBase
{
    [HttpGet("stream")]
    public Task Stream(
        string workspaceId,
        string application,
        [FromQuery] string[]? kinds,
        [FromQuery] string[]? streams,
        CancellationToken cancellationToken)
        => stream.WriteAsync(Response, workspaceId, application, kinds, streams, cancellationToken);
}
