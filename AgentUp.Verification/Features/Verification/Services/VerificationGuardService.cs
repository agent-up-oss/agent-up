using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>
/// Answers the end-of-run question: is every check these changes require proven against
/// the bytes that are there right now?
/// </summary>
/// <remarks>
/// This does not run anything. It hashes changed files and reads a JSON ledger, so it is
/// cheap enough for a Stop hook that fires on every turn end. Execution is an explicit
/// act through <see cref="VerificationRunService"/>.
/// </remarks>
public sealed class VerificationGuardService(
    VerificationPlanService plans,
    IReceiptLedgerStore ledgers)
{
    public async Task<GuardReport> GuardAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var configuration = plans.LoadConfiguration(repositoryRoot);
        var plan = await plans.CreatePlanAsync(repositoryRoot, cancellationToken);

        if (!plan.HasWork && plan.UnmatchedFiles.Count == 0)
            return GuardReport.Clean with { Enforcement = configuration.Enforcement };

        var ledger = await ledgers.ReadAsync(repositoryRoot, cancellationToken);

        var verdicts = plan.Checks
            .Select(check => Judge(check, ledger.Find(check.CheckId)))
            .ToArray();

        return new GuardReport(
            verdicts,
            configuration.Enforcement,
            plan.UnmatchedFiles,
            plan.ChangedFiles.Count);
    }

    private static CheckVerdict Judge(PlannedCheck check, VerificationReceipt? receipt)
    {
        if (!check.Runnable)
        {
            return new CheckVerdict(
                check.CheckId,
                check.Definition.Command,
                CheckVerdictKind.Skipped,
                SkipDetail(check));
        }

        if (receipt is null)
        {
            return new CheckVerdict(
                check.CheckId,
                check.Definition.Command,
                CheckVerdictKind.NeverRun,
                $"Required by {Describe(check.SelectedBy)} but has not run.");
        }

        if (!receipt.Succeeded)
        {
            return new CheckVerdict(
                check.CheckId,
                check.Definition.Command,
                CheckVerdictKind.Failed,
                $"Last run exited {receipt.ExitCode} at {receipt.RanAtUtc}.");
        }

        if (!string.Equals(receipt.Command, check.Definition.Command, StringComparison.Ordinal))
        {
            return new CheckVerdict(
                check.CheckId,
                check.Definition.Command,
                CheckVerdictKind.Stale,
                $"The receipt proves a different command ('{receipt.Command}').");
        }

        var drift = FindDrift(check.Covered, receipt.Covered);
        if (drift is not null)
        {
            return new CheckVerdict(
                check.CheckId,
                check.Definition.Command,
                CheckVerdictKind.Stale,
                drift);
        }

        return new CheckVerdict(
            check.CheckId,
            check.Definition.Command,
            CheckVerdictKind.Satisfied,
            $"Proven against {check.Covered.Count} changed file(s) at {receipt.RanAtUtc}.");
    }

    /// <summary>
    /// Compares what must be covered now against what the receipt actually covered.
    /// Any difference in either direction is staleness: a file changed after the run, a
    /// file was added to the change set, or a file left it.
    /// </summary>
    private static string? FindDrift(
        IReadOnlyDictionary<string, string> required,
        IReadOnlyDictionary<string, string> proven)
    {
        var changed = required
            .Where(entry => proven.TryGetValue(entry.Key, out var hash) && !string.Equals(hash, entry.Value, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (changed.Length > 0)
            return $"{Describe(changed)} changed after the check ran.";

        var added = required.Keys
            .Where(path => !proven.ContainsKey(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (added.Length > 0)
            return $"{Describe(added)} was not covered by the last run.";

        var removed = proven.Keys
            .Where(path => !required.ContainsKey(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return removed.Length > 0
            ? $"The change set no longer includes {Describe(removed)}, so the proof no longer matches."
            : null;
    }

    private static string SkipDetail(PlannedCheck check)
        => check.SkipReason switch
        {
            CheckSkipReason.PlatformMismatch =>
                $"Requires {string.Join(" or ", check.Definition.Platforms)}; not runnable here. Still required in CI.",
            CheckSkipReason.CiOnly =>
                "Marked ciOnly; runs in CI only.",
            _ => "Skipped."
        };

    private static string Describe(IReadOnlyList<string> items)
        => items.Count switch
        {
            0 => "nothing",
            1 => $"'{items[0]}'",
            2 => $"'{items[0]}' and '{items[1]}'",
            _ => $"'{items[0]}' and {items.Count - 1} other(s)"
        };
}
