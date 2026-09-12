using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

public sealed class VerifyCoverageCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(
        string worktreePath,
        double? minimumOverride,
        CancellationToken cancellationToken = default)
        => output.WriteCoverage(await service.CoverageAsync(worktreePath, minimumOverride, cancellationToken));
}
