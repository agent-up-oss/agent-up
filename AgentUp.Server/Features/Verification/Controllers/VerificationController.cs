using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Features.Verification.Services;

namespace AgentUp.Server.Features.Verification.Controllers;

public sealed class VerificationController(VerificationQueueGateService service)
{
    public Task<VerificationGateResult> RunAndGuardAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.RunAndGuardAsync(worktreePath, cancellationToken);
}
