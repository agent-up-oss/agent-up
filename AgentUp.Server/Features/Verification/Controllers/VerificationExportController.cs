using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Verification.Features.Verification.Services;

namespace AgentUp.Server.Features.Verification.Controllers;

public sealed class VerificationExportController(
    VerificationExportService exports,
    VerificationReportService reports)
{
    public async Task<VerificationExportDto> ExportAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var export = await exports.ExportAsync(worktreePath, cancellationToken);
        return reports.ToExportDto(export);
    }
}
