using AgentUp.CLI.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Controllers;

public sealed class VerifyPlanCommand(VerifyCommandService service, VerifyOutputService output)
{
    public async Task<int> RunAsync(string worktreePath, CancellationToken cancellationToken = default)
        => output.WritePlan(await service.PlanAsync(worktreePath, cancellationToken));
}
