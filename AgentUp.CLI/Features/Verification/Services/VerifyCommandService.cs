using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Services;

namespace AgentUp.CLI.Features.Verification.Services;

/// <summary>
/// The behaviour behind <c>agentup verify</c>: resolve, execute, guard, and turn a broken
/// configuration into a reportable error instead of an exception.
/// </summary>
public sealed class VerifyCommandService(
    VerificationPlanService plans,
    VerificationRunService runs,
    VerificationGuardService guards)
{
    public async Task<VerifyPlanResult> PlanAsync(string worktreePath, CancellationToken cancellationToken)
    {
        try
        {
            return new VerifyPlanResult(await plans.CreatePlanAsync(worktreePath, cancellationToken), null);
        }
        catch (VerificationConfigurationException exception)
        {
            return new VerifyPlanResult(null, exception.Message);
        }
    }

    public async Task<VerifyRunResult> RunAsync(
        string worktreePath,
        string? checkId,
        CancellationToken cancellationToken)
    {
        try
        {
            return new VerifyRunResult(await ExecuteAsync(worktreePath, checkId, cancellationToken), null);
        }
        catch (VerificationConfigurationException exception)
        {
            return new VerifyRunResult([], exception.Message);
        }
    }

    /// <summary>
    /// Guards, and when <paramref name="runMissing"/> is set, executes what is unproven and
    /// guards again.
    /// </summary>
    /// <remarks>
    /// Running is opt-in because the default guard is wired into a Stop hook that fires on
    /// every turn end: executing suites there would make each turn end slow and turn a hook
    /// timeout into a false failure.
    /// </remarks>
    public async Task<VerifyGuardResult> GuardAsync(
        string worktreePath,
        bool runMissing,
        CancellationToken cancellationToken)
    {
        try
        {
            return new VerifyGuardResult(await GuardReportAsync(worktreePath, runMissing, cancellationToken), null);
        }
        catch (VerificationConfigurationException exception)
        {
            return new VerifyGuardResult(null, exception.Message);
        }
    }

    private async Task<GuardReport> GuardReportAsync(
        string worktreePath,
        bool runMissing,
        CancellationToken cancellationToken)
    {
        var report = await guards.GuardAsync(worktreePath, cancellationToken);
        if (report.Satisfied || !runMissing)
            return report;

        await runs.RunAsync(worktreePath, cancellationToken);
        return await guards.GuardAsync(worktreePath, cancellationToken);
    }

    private async Task<IReadOnlyList<CheckOutcome>> ExecuteAsync(
        string worktreePath,
        string? checkId,
        CancellationToken cancellationToken)
    {
        if (checkId is null)
            return await runs.RunAsync(worktreePath, cancellationToken);

        var outcome = await runs.RunSingleAsync(worktreePath, checkId, cancellationToken);
        return outcome is null ? [] : [outcome];
    }
}
