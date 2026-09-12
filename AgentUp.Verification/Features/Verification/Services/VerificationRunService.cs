using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>
/// Executes the checks the current changes require and records a receipt for each.
/// </summary>
/// <remarks>
/// The caller chooses when to run, never what to run: the plan comes from the static
/// rules. A receipt is written for failures as well as successes, so a failing check is
/// reported as failed rather than as never run.
/// </remarks>
public sealed class VerificationRunService(
    VerificationPlanService plans,
    IReceiptLedgerStore ledgers,
    ICheckRunner runner,
    IVerificationClock clock)
{
    public async Task<IReadOnlyList<CheckOutcome>> RunAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var plan = await plans.CreatePlanAsync(repositoryRoot, cancellationToken);
        var runnable = plan.Checks.Where(check => check.Runnable).ToArray();

        var outcomes = new List<CheckOutcome>();

        foreach (var check in runnable)
        {
            var outcome = await runner.RunAsync(repositoryRoot, check.Definition, cancellationToken);
            outcomes.Add(outcome);
            await RecordAsync(repositoryRoot, check, outcome, cancellationToken);

            if (!outcome.Succeeded)
                break;
        }

        return outcomes;
    }

    /// <summary>
    /// Runs one named check regardless of whether the current changes require it, and
    /// records its receipt. Used to re-prove a single check after a targeted fix.
    /// </summary>
    public async Task<CheckOutcome?> RunSingleAsync(
        string repositoryRoot,
        string checkId,
        CancellationToken cancellationToken = default)
    {
        var plan = await plans.CreatePlanAsync(repositoryRoot, cancellationToken);
        var check = plan.Checks.FirstOrDefault(planned =>
            string.Equals(planned.CheckId, checkId, StringComparison.Ordinal));

        if (check is null || !check.Runnable)
            return null;

        var outcome = await runner.RunAsync(repositoryRoot, check.Definition, cancellationToken);
        await RecordAsync(repositoryRoot, check, outcome, cancellationToken);
        return outcome;
    }

    private async Task RecordAsync(
        string repositoryRoot,
        PlannedCheck check,
        CheckOutcome outcome,
        CancellationToken cancellationToken)
    {
        var receipt = new VerificationReceipt(
            check.CheckId,
            outcome.Command,
            outcome.ExitCode,
            clock.UtcNow.ToString("O"),
            outcome.DurationMs,
            check.Covered);

        var ledger = await ledgers.ReadAsync(repositoryRoot, cancellationToken);
        await ledgers.WriteAsync(repositoryRoot, ledger.With(receipt), cancellationToken);
    }
}
