using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// Executes one planned check. Separated from the services so the resolution and receipt
/// logic is testable without starting processes.
/// </summary>
public interface ICheckRunner
{
    Task<CheckOutcome> RunAsync(
        string repositoryRoot,
        CheckDefinition check,
        CancellationToken cancellationToken);
}
