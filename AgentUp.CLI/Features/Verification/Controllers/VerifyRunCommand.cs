using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

public sealed class VerifyRunCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(
        string worktreePath,
        string? checkId,
        CancellationToken cancellationToken = default)
        => output.WriteRun(await service.RunAsync(worktreePath, checkId, cancellationToken));
}
