using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Verification.Features.Verification.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Verification.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/verification")]
public sealed class VerificationHttpController(
    WorkspaceQueryController workspaces,
    VerificationExportService exports,
    VerificationReportService reports) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return NotFound();

        var export = await exports.ExportAsync(workspace.WorktreePath, cancellationToken);
        return Ok(reports.ToExportDto(export));
    }
}
