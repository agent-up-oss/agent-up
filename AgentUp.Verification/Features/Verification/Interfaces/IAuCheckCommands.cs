using AgentUp.Verification.Features.Verification.DTOs;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>The portable runner's commands. The controller only forwards an invocation.</summary>
public interface IAuCheckCommands
{
    Task<int> ExecuteAsync(AuCheckInvocation invocation, CancellationToken cancellationToken);
}
