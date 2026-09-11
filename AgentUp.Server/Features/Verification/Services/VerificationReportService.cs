using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Server.Features.Verification.Services;

/// <summary>
/// Projects verification results into the MCP-facing shapes.
/// </summary>
/// <remarks>
/// Kept separate from the engine so the wire vocabulary ("neverRun", "stale") can change
/// without touching resolution or receipt logic.
/// </remarks>
public sealed class VerificationReportService
{
    public VerificationPlanDto ToPlanDto(VerificationPlan plan)
        => new(
            plan.ChangedFiles.Count,
            plan.UnmatchedFiles,
            [.. plan.Checks.Select(ToCheckDto)]);

    public VerificationGuardDto ToGuardDto(GuardReport report, VerificationPlan plan)
    {
        var byId = plan.Checks.ToDictionary(check => check.CheckId, StringComparer.Ordinal);

        return new VerificationGuardDto(
            report.Satisfied,
            report.ShouldBlock,
            Describe(report.Enforcement),
            report.ChangedFileCount,
            report.UnmatchedFiles,
            [.. report.BlockingVerdicts.Select(verdict => ToCheckDto(verdict, byId))],
            [.. report.SkippedVerdicts.Select(verdict => ToCheckDto(verdict, byId))],
            [.. report.Verdicts
                .Where(verdict => verdict.Kind == CheckVerdictKind.Satisfied)
                .Select(verdict => ToCheckDto(verdict, byId))]);
    }

    public VerificationRunDto ToRunDto(IReadOnlyList<CheckOutcome> outcomes)
        => new(
            outcomes.All(outcome => outcome.Succeeded),
            [.. outcomes.Select(outcome => new VerificationOutcomeDto(
                outcome.CheckId,
                outcome.Command,
                outcome.ExitCode,
                outcome.DurationMs,
                Tail(outcome.Output)))]);

    /// <summary>
    /// A single sentence an agent can act on without reading anything else.
    /// </summary>
    public string Summarize(GuardReport report)
    {
        if (report.ChangedFileCount == 0)
            return "Nothing changed, so no checks are required.";

        if (report.Satisfied)
        {
            var skipped = report.SkippedVerdicts.Count > 0
                ? $" {report.SkippedVerdicts.Count} check(s) skipped on this platform and still required in CI."
                : string.Empty;
            return $"Every check required by {report.ChangedFileCount} changed file(s) is proven.{skipped}";
        }

        var unmatched = report.UnmatchedFiles.Count > 0
            ? $" {report.UnmatchedFiles.Count} changed file(s) match no path rule, so the verification map is incomplete."
            : string.Empty;

        var blocking = string.Join(", ", report.BlockingVerdicts.Select(verdict => verdict.CheckId));
        var action = report.ShouldBlock ? "Run run_verification" : "Enforcement is warn; run run_verification";

        return $"Unproven check(s): {blocking}.{unmatched} {action} to prove them.";
    }

    private static VerificationCheckDto ToCheckDto(PlannedCheck check)
        => new(
            check.CheckId,
            check.Definition.Command,
            check.Runnable ? "required" : "skipped",
            check.Runnable ? $"Required by {string.Join(", ", check.SelectedBy)}" : DescribeSkip(check.SkipReason),
            check.SelectedBy,
            check.Covered.Count);

    private static VerificationCheckDto ToCheckDto(
        CheckVerdict verdict,
        IReadOnlyDictionary<string, PlannedCheck> byId)
    {
        var planned = byId.TryGetValue(verdict.CheckId, out var check) ? check : null;
        return new VerificationCheckDto(
            verdict.CheckId,
            verdict.Command,
            Describe(verdict.Kind),
            verdict.Detail,
            planned?.SelectedBy ?? [],
            planned?.Covered.Count ?? 0);
    }

    private static string DescribeSkip(CheckSkipReason reason)
        => reason switch
        {
            CheckSkipReason.PlatformMismatch => "Not runnable on this platform; still required in CI.",
            CheckSkipReason.CiOnly => "Runs in CI only.",
            _ => "Skipped."
        };

    private static string Describe(CheckVerdictKind kind)
        => kind switch
        {
            CheckVerdictKind.Satisfied => "satisfied",
            CheckVerdictKind.NeverRun => "neverRun",
            CheckVerdictKind.Failed => "failed",
            CheckVerdictKind.Stale => "stale",
            _ => "skipped"
        };

    private static string Describe(VerificationEnforcement enforcement)
        => enforcement == VerificationEnforcement.Block ? "block" : "warn";

    private static string Tail(string output)
    {
        const int limit = 4000;
        return output.Length <= limit ? output : output[^limit..];
    }
}
