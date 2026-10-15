using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

/// <summary>
/// Emits a report of what the current working tree has proven. Cheap: it plans, guards,
/// and reads the ledger without executing anything.
/// </summary>
public sealed class VerifyExportCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(
        string worktreePath,
        VerifyOutputFormat format,
        CancellationToken cancellationToken = default)
        => output.WriteExport(await service.ExportAsync(worktreePath, cancellationToken), format);
}
