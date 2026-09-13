using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Services;

namespace AgentUp.Server.Features.Verification.Services;

public sealed class VerificationQueueGateService(VerificationRunService runs, VerificationGuardService guards)
{
    public async Task<VerificationGateResult> RunAndGuardAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var outcomes = await runs.RunAsync(worktreePath, cancellationToken);
            if (outcomes.LastOrDefault() is { Succeeded: false } failed)
                return new VerificationGateResult(false, $"Verification check '{failed.CheckId}' failed with exit code {failed.ExitCode}.");

            var report = await guards.GuardAsync(worktreePath, cancellationToken);
            return report.Satisfied
                ? new VerificationGateResult(true, "Every required verification check is proven for this proposal.")
                : new VerificationGateResult(false, "Verification is incomplete for this proposal. Run the required checks and complete the verification path map.");
        }
        catch (VerificationConfigurationException exception)
        {
            return new VerificationGateResult(false, exception.Message);
        }
        catch (IOException)
        {
            return new VerificationGateResult(false, "Verification receipts could not be recorded.");
        }
        catch (UnauthorizedAccessException)
        {
            return new VerificationGateResult(false, "Verification receipts could not be recorded.");
        }
    }
}
