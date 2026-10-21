using AgentUp.Verification.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Features.Verification.Controllers;

/// <summary>Inbound boundary for <c>au-check</c>. It forwards one invocation and returns the exit code.</summary>
public sealed class AuCheckController(IAuCheckCommands commands)
{
    public Task<int> RunAsync(AuCheckInvocation invocation, CancellationToken cancellationToken = default)
        => commands.ExecuteAsync(invocation, cancellationToken);
}
