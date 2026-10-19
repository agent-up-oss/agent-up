using AgentUp.Verification.Features.Coverage.Controllers;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>
/// Hosts plan, run, guard, coverage, and slices for <c>au-check</c>. Resolution stays in
/// <see cref="CheckPlanProvider"/>; this service only prints and maps outcomes to exit codes.
/// </summary>
public sealed class AuCheckCommandService(
    VerificationPlanService plans,
    VerificationRunService runs,
    VerificationGuardService guards,
    PatchCoverageController coverage,
    SliceCoverageController slices,
    IGitRevisionProvider revisions,
    ContentHashProvider hashes,
    IRunReportWriter reports,
    string toolVersion,
    TextWriter output,
    TextWriter error) : IAuCheckCommands
{
    public async Task<int> ExecuteAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        if (invocation.Error is not null)
        {
            error.WriteLine(invocation.Error);
            return AuCheckExit.Invalid;
        }

        if (invocation.Command == "help")
        {
            output.WriteLine(AuCheckHelp.Text);
            return AuCheckExit.Passed;
        }

        try
        {
            return invocation.Command switch
            {
                "plan" => await PlanAsync(invocation, cancellationToken),
                "run" => await RunAsync(invocation, cancellationToken),
                "guard" => await GuardAsync(invocation, cancellationToken),
                "coverage" => await CoverageAsync(invocation, cancellationToken),
                "slices" => await SlicesAsync(invocation, cancellationToken),
                _ => Unknown(invocation.Command)
            };
        }
        catch (VerificationConfigurationException exception)
        {
            error.WriteLine(exception.Message);
            return AuCheckExit.Invalid;
        }
        catch (CoverageConfigurationException exception)
        {
            error.WriteLine(exception.Message);
            return AuCheckExit.Invalid;
        }
    }

    private async Task<int> PlanAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        var plan = await plans.CreatePlanAsync(invocation.RepositoryRoot, cancellationToken);
        VerificationConsoleText.WritePlan(output, plan);
        var (code, reason) = AuCheckExit.ForPlan(plan);
        await WriteReportAsync(invocation, plan, [], code, reason, cancellationToken);
        return code;
    }

    private async Task<int> RunAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        var plan = await plans.CreatePlanAsync(invocation.RepositoryRoot, cancellationToken);
        var outcomes = await ExecuteAsync(invocation, plan, cancellationToken);
        WriteRun(outcomes);
        var (code, reason) = AuCheckExit.ForRun(plan, outcomes);
        if (plan.UnmatchedFiles.Count > 0)
            VerificationConsoleText.WriteUnmatched(error, plan.UnmatchedFiles);

        await WriteReportAsync(invocation, plan, outcomes, code, reason, cancellationToken);
        return code;
    }

    private async Task<IReadOnlyList<CheckOutcome>> ExecuteAsync(
        AuCheckInvocation invocation,
        VerificationPlan plan,
        CancellationToken cancellationToken)
    {
        if (invocation.CheckId is null)
            return await runs.RunAsync(invocation.RepositoryRoot, cancellationToken);

        if (plan.Checks.All(check => !string.Equals(check.CheckId, invocation.CheckId, StringComparison.Ordinal)))
            return [];

        var outcome = await runs.RunSingleAsync(invocation.RepositoryRoot, invocation.CheckId, cancellationToken);
        return outcome is null ? [] : [outcome];
    }

    private async Task<int> GuardAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        var report = await guards.GuardAsync(invocation.RepositoryRoot, cancellationToken);
        if (!report.Satisfied && invocation.RunMissing)
        {
            await runs.RunAsync(invocation.RepositoryRoot, cancellationToken);
            report = await guards.GuardAsync(invocation.RepositoryRoot, cancellationToken);
        }

        WriteGuard(report);
        var (code, reason) = AuCheckExit.ForGuard(report);
        if (invocation.ReportDirectory is not null)
        {
            var plan = await plans.CreatePlanAsync(invocation.RepositoryRoot, cancellationToken);
            await WriteReportAsync(invocation, plan, [], code, reason, cancellationToken);
        }

        return code;
    }

    private async Task<int> CoverageAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        var result = await coverage.MeasureAsync(invocation.RepositoryRoot, invocation.Minimum, cancellationToken);
        return WriteCoverage(result);
    }

    private async Task<int> SlicesAsync(AuCheckInvocation invocation, CancellationToken cancellationToken)
    {
        var result = await slices.MeasureAsync(invocation.RepositoryRoot, invocation.Minimum, cancellationToken);
        return WriteSlices(result);
    }

    private async Task WriteReportAsync(
        AuCheckInvocation invocation,
        VerificationPlan plan,
        IReadOnlyList<CheckOutcome> outcomes,
        int code,
        string reason,
        CancellationToken cancellationToken)
    {
        if (invocation.ReportDirectory is null)
            return;

        var hash = hashes.HashFile(invocation.ConfigPath);
        var report = RunReportBuilder.Build(
            toolVersion,
            invocation.ConfigPath,
            hash,
            revisions.Resolve(invocation.RepositoryRoot),
            plan,
            outcomes,
            code,
            reason);

        try
        {
            await reports.WriteAsync(invocation.ReportDirectory, report, cancellationToken);
            output.WriteLine($"Report written to {invocation.ReportDirectory}");
        }
        catch (ReportCaptureException exception)
        {
            error.WriteLine(exception.Message);
        }
    }

    private void WriteRun(IReadOnlyList<CheckOutcome> outcomes)
    {
        if (outcomes.Count == 0)
        {
            output.WriteLine("Nothing to run: no check is required by the current changes.");
            return;
        }

        foreach (var outcome in outcomes)
            output.WriteLine($"  {(outcome.Succeeded ? "PASS" : "FAIL")} {outcome.CheckId} ({outcome.DurationMs} ms)");

        var failed = outcomes.FirstOrDefault(outcome => !outcome.Succeeded);
        if (failed is null)
        {
            output.WriteLine($"{outcomes.Count} check(s) passed and were recorded.");
            return;
        }

        error.WriteLine($"Check '{failed.CheckId}' failed with exit code {failed.ExitCode}.");
        error.WriteLine(failed.Output);
    }

    private void WriteGuard(GuardReport report)
    {
        if (report.ChangedFileCount == 0)
        {
            output.WriteLine("Nothing changed, so no checks are required.");
            return;
        }

        foreach (var verdict in report.Verdicts)
            output.WriteLine($"  {(verdict.Blocking ? "FAIL" : "PASS")} {verdict.CheckId}: {verdict.Detail}");

        VerificationConsoleText.WriteUnmatched(report.UnmatchedFiles.Count == 0 ? output : error, report.UnmatchedFiles);

        if (report.Satisfied)
        {
            output.WriteLine($"Every check required by {report.ChangedFileCount} changed file(s) is proven.");
            return;
        }

        var target = report.ShouldBlock ? error : output;
        target.WriteLine(report.ShouldBlock
            ? "Unproven checks remain. Run 'au-check run'."
            : "Unproven checks remain (enforcement is warn). Run 'au-check run'.");
    }

    private int WriteCoverage(PatchCoverageResult result)
    {
        if (result.FilesWithoutReport.Count > 0)
        {
            error.WriteLine($"{result.FilesWithoutReport.Count} changed file(s) have no coverage report.");
            return AuCheckExit.Failed;
        }

        if (result.CoverableLines == 0)
        {
            output.WriteLine("No changed lines are coverable, so patch coverage does not apply.");
            return AuCheckExit.Passed;
        }

        output.WriteLine(
            $"Patch coverage {result.Percentage:0.##}% ({result.CoveredLines}/{result.CoverableLines} changed line(s)), minimum {result.Minimum:0.##}%.");
        return result.Satisfied ? AuCheckExit.Passed : AuCheckExit.Failed;
    }

    private int WriteSlices(SliceCoverageResult result)
    {
        if (result.MeasuredNothing)
        {
            error.WriteLine("No coverage report mentions any feature slice.");
            return AuCheckExit.Failed;
        }

        output.WriteLine($"{result.Slices.Count} feature slice(s), floor {result.Minimum:0.##}%.");
        return result.IsSatisfied ? AuCheckExit.Passed : AuCheckExit.Failed;
    }

    private int Unknown(string command)
    {
        error.WriteLine($"Unknown command '{command}'.");
        return AuCheckExit.Invalid;
    }
}
