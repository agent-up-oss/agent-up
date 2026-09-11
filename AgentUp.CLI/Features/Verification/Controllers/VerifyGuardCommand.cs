using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

/// <summary>
/// The end-of-run guard. Cheap by default: it hashes changed files and reads the receipt
/// ledger without executing anything, so it is safe in a Stop hook.
/// </summary>
public sealed class VerifyGuardCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(
        string worktreePath,
        VerifyOutputFormat format,
        bool runMissing,
        CancellationToken cancellationToken = default)
        => output.WriteGuard(await service.GuardAsync(worktreePath, runMissing, cancellationToken), format);
}
