using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.CLI.Features.Verification.Services;

/// <summary>
/// Renders verification results for the CLI.
/// </summary>
public sealed class VerifyOutputService(TextWriter output, TextWriter error)
{
    private const int BlockingExitCode = 2;

    public int WritePlan(VerifyPlanResult result)
        => result.Plan is null ? WriteError(result.Error ?? "Verification failed.") : WritePlan(result.Plan);

    public int WriteRun(VerifyRunResult result)
        => result.Error is null ? WriteRun(result.Outcomes) : WriteError(result.Error);

    public int WriteGuard(VerifyGuardResult result, VerifyOutputFormat format)
        => result.Report is null ? WriteError(result.Error ?? "Verification failed.") : WriteGuard(result.Report, format);

    private int WritePlan(VerificationPlan plan)
    {
        if (plan.ChangedFiles.Count == 0)
        {
            output.WriteLine("Nothing changed, so no checks are required.");
            return 0;
        }

        output.WriteLine($"{plan.Checks.Count} check(s) required by {plan.ChangedFiles.Count} changed file(s):");
        foreach (var line in plan.Checks.Select(FormatPlannedCheck))
            output.WriteLine(line);

        WriteUnmatched(plan.UnmatchedFiles, output);
        return 0;
    }

    private int WriteRun(IReadOnlyList<CheckOutcome> outcomes)
    {
        if (outcomes.Count == 0)
        {
            output.WriteLine("Nothing to run: no check is required by the current changes.");
            return 0;
        }

        foreach (var outcome in outcomes)
            output.WriteLine($"  {Mark(outcome.Succeeded)} {outcome.CheckId} ({outcome.DurationMs} ms)");

        var failed = outcomes.FirstOrDefault(outcome => !outcome.Succeeded);
        if (failed is null)
        {
            output.WriteLine($"{outcomes.Count} check(s) passed and were recorded.");
            return 0;
        }

        error.WriteLine($"Check '{failed.CheckId}' failed with exit code {failed.ExitCode}.");
        error.WriteLine(failed.Output);
        return 1;
    }

    private int WriteGuard(GuardReport report, VerifyOutputFormat format)
        => format == VerifyOutputFormat.Hook ? WriteGuardForHook(report) : WriteGuardAsText(report);

    public int WriteCoverage(VerifyCoverageResult result)
        => result.Coverage is null
            ? WriteError(result.Error ?? "Coverage measurement failed.")
            : WriteCoverage(result.Coverage);

    public int WriteSliceCoverage(VerifySliceCoverageResult result)
        => result.Coverage is null
            ? WriteError(result.Error ?? "Slice coverage measurement failed.")
            : WriteSliceCoverage(result.Coverage);

    private int WriteSliceCoverage(SliceCoverageResult coverage)
    {
        if (coverage.MeasuredNothing)
        {
            error.WriteLine(
                "No coverage report mentions any feature slice. Run the tests with coverage "
                + "collection first.");
            return 1;
        }

        output.WriteLine(
            $"{coverage.Slices.Count} feature slice(s), floor {coverage.Minimum:0.##}%, worst first:");

        foreach (var slice in coverage.Slices)
        {
            output.WriteLine(
                $"    {slice.Percent,6:0.0}%  {slice.Covered,5}/{slice.Coverable,-5}  {slice.Slice}"
                + (slice.Meets(coverage.Minimum) ? string.Empty : "  (below floor)"));
        }

        if (coverage.IsSatisfied)
            return 0;

        foreach (var slice in coverage.Failing)
        {
            error.WriteLine(
                $"{slice.Slice} is at {slice.Percent:0.##}%, below the required "
                + $"{coverage.Minimum:0.##}%.");
        }

        foreach (var slice in coverage.ResolvedExemptions)
        {
            error.WriteLine(
                $"{slice} now reaches the floor. Remove it from 'coverage.sliceExemptions'.");
        }

        return 1;
    }

    private int WriteCoverage(PatchCoverageResult coverage)
    {
        if (coverage.FilesWithoutReport.Count > 0)
        {
            error.WriteLine(
                $"{coverage.FilesWithoutReport.Count} changed file(s) have no coverage report. " +
                "Run the tests with coverage collection first:");
            foreach (var path in coverage.FilesWithoutReport)
                error.WriteLine($"    {path}");
            return 1;
        }

        if (coverage.CoverableLines == 0)
        {
            output.WriteLine("No changed lines are coverable, so patch coverage does not apply.");
            return 0;
        }

        output.WriteLine(
            $"Patch coverage {coverage.Percentage:0.##}% " +
            $"({coverage.CoveredLines}/{coverage.CoverableLines} changed line(s)), " +
            $"minimum {coverage.Minimum:0.##}%.");

        foreach (var file in coverage.Uncovered)
            output.WriteLine($"    {file.Path}: {file.DescribeRanges()}");

        if (coverage.Satisfied)
            return 0;

        error.WriteLine(
            $"Patch coverage {coverage.Percentage:0.##}% is below the required {coverage.Minimum:0.##}%.");
        return 1;
    }

    public int WriteError(string message)
    {
        error.WriteLine(message);
        return 1;
    }

    private int WriteGuardAsText(GuardReport report)
    {
        if (report.ChangedFileCount == 0)
        {
            output.WriteLine("Nothing changed, so no checks are required.");
            return 0;
        }

        foreach (var verdict in report.Verdicts)
            output.WriteLine($"  {Mark(!verdict.Blocking)} {verdict.CheckId}: {verdict.Detail}");

        WriteUnmatched(report.UnmatchedFiles, output);

        if (report.Satisfied)
        {
            output.WriteLine($"Every check required by {report.ChangedFileCount} changed file(s) is proven.");
            return 0;
        }

        var target = report.ShouldBlock ? error : output;
        target.WriteLine(report.ShouldBlock
            ? "Unproven checks remain. Run 'agentup verify run'."
            : "Unproven checks remain (enforcement is warn). Run 'agentup verify run'.");

        return report.ShouldBlock ? BlockingExitCode : 0;
    }

    /// <summary>
    /// Silent when there is nothing to say. A guard that prints on every success trains
    /// everyone to ignore it.
    /// </summary>
    private int WriteGuardForHook(GuardReport report)
    {
        if (report.Satisfied || !report.ShouldBlock)
            return 0;

        error.WriteLine("[agent-up] Verification is incomplete for the changes in this run:");
        foreach (var verdict in report.BlockingVerdicts)
            error.WriteLine($"[agent-up]   {verdict.CheckId} - {verdict.Detail}");

        WriteUnmatched(report.UnmatchedFiles, error, "[agent-up]   ");
        error.WriteLine("[agent-up] Call the run_verification MCP tool, or run 'agentup verify run'.");
        return BlockingExitCode;
    }

    private static void WriteUnmatched(IReadOnlyList<string> unmatched, TextWriter writer, string prefix = "  ")
    {
        if (unmatched.Count == 0)
            return;

        writer.WriteLine($"{prefix}{unmatched.Count} changed file(s) match no path rule, so the verification map is incomplete:");
        foreach (var path in unmatched)
            writer.WriteLine($"{prefix}  {path}");
    }

    private static string FormatPlannedCheck(PlannedCheck check)
    {
        var selection = string.Join(", ", check.SelectedBy);
        return check.Runnable
            ? $"  {check.CheckId}: {check.Definition.Command}  [{selection}]"
            : $"  {check.CheckId}: skipped here ({check.SkipReason}), still required in CI  [{selection}]";
    }

    private static string Mark(bool ok) => ok ? "PASS" : "FAIL";
}
