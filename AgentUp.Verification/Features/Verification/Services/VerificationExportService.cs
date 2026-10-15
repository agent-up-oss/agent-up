using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>
/// Builds a report of what the current working tree has proven. This runs nothing: it plans,
/// guards, and reads the ledger the same way <see cref="VerificationGuardService"/> already does.
/// </summary>
public sealed class VerificationExportService(
    VerificationPlanService plans,
    VerificationGuardService guards,
    IReceiptLedgerStore ledgers,
    IVerificationClock clock,
    IGitRevisionProvider revisions,
    CoveredDigestProvider digests)
{
    public async Task<VerificationExport> ExportAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var plan = await plans.CreatePlanAsync(repositoryRoot, cancellationToken);
        var report = await guards.GuardAsync(repositoryRoot, cancellationToken);
        var ledger = await ledgers.ReadAsync(repositoryRoot, cancellationToken);
        var covered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var check in plan.Checks)
        {
            foreach (var (path, hash) in check.Covered)
                covered[path] = hash;
        }

        var byId = plan.Checks.ToDictionary(check => check.CheckId, StringComparer.Ordinal);
        var checks = report.Verdicts
            .Select(verdict => ToCheck(verdict, ledger.Find(verdict.CheckId), byId))
            .ToArray();

        return new VerificationExport(
            revisions.Resolve(repositoryRoot),
            digests.Digest(covered),
            clock.UtcNow.ToString("O"),
            report.ChangedFileCount,
            report.UnmatchedFiles,
            checks);
    }

    private static VerificationExportCheck ToCheck(
        CheckVerdict verdict,
        VerificationReceipt? receipt,
        IReadOnlyDictionary<string, PlannedCheck> byId)
    {
        var planned = byId.GetValueOrDefault(verdict.CheckId);
        return new VerificationExportCheck(
            verdict.CheckId,
            verdict.Command,
            Describe(verdict.Kind),
            verdict.Detail,
            receipt?.ExitCode,
            receipt?.RanAtUtc,
            receipt?.DurationMs,
            DescribeSkip(planned?.SkipReason ?? CheckSkipReason.None));
    }

    private static string Describe(CheckVerdictKind kind)
        => kind switch
        {
            CheckVerdictKind.Satisfied => "satisfied",
            CheckVerdictKind.NeverRun => "neverRun",
            CheckVerdictKind.Failed => "failed",
            CheckVerdictKind.Stale => "stale",
            _ => "skipped"
        };

    private static string DescribeSkip(CheckSkipReason reason)
        => reason switch
        {
            CheckSkipReason.PlatformMismatch => "platformMismatch",
            CheckSkipReason.CiOnly => "ciOnly",
            _ => ""
        };
}
