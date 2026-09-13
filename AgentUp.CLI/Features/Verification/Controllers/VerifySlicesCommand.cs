using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

public sealed class VerifySlicesCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(
        string worktreePath,
        double? minimumOverride,
        CancellationToken cancellationToken = default)
        => output.WriteSliceCoverage(await service.SliceCoverageAsync(worktreePath, minimumOverride, cancellationToken));
}
