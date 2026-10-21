using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Services;

/// <summary>Builds the run report from a plan and the outcomes that actually executed.</summary>
public static class RunReportBuilder
{
    public static RunReport Build(
        string version,
        string configPath,
        string configHash,
        string commit,
        VerificationPlan plan,
        IReadOnlyList<CheckOutcome> outcomes,
        int exitCode,
        string exitReason)
    {
        var byId = outcomes.ToDictionary(outcome => outcome.CheckId, StringComparer.Ordinal);
        var checks = plan.Checks.Select(check => ToCheck(check, byId)).ToArray();
        return new RunReport(
            version,
            configPath,
            configHash,
            commit,
            plan.ChangedFiles.Keys.Order(StringComparer.Ordinal).ToArray(),
            checks,
            outcomes.Count,
            outcomes.Count(outcome => outcome.Succeeded),
            outcomes.Count(outcome => !outcome.Succeeded),
            plan.Checks.Count(check => !check.Runnable),
            exitCode,
            exitReason);
    }

    private static RunReportCheck ToCheck(PlannedCheck planned, IReadOnlyDictionary<string, CheckOutcome> outcomes)
    {
        if (!planned.Runnable)
        {
            return new RunReportCheck(
                planned.CheckId,
                Tier(planned.Definition.Tier),
                planned.Definition.Command,
                "skipped",
                null,
                Skip(planned.SkipReason),
                planned.Toolchain?.Describe() ?? (planned.SkipReason == CheckSkipReason.ToolchainMissing ? planned.SkipReason.ToString() : null),
                null);
        }

        if (!outcomes.TryGetValue(planned.CheckId, out var outcome))
        {
            return new RunReportCheck(
                planned.CheckId,
                Tier(planned.Definition.Tier),
                planned.Definition.Command,
                "notRun",
                null,
                null,
                null,
                null);
        }

        return new RunReportCheck(
            planned.CheckId,
            Tier(planned.Definition.Tier),
            outcome.Command,
            outcome.Succeeded ? "passed" : "failed",
            outcome.DurationMs,
            null,
            null,
            outcome.Output);
    }

    private static string Tier(CheckTier tier)
        => tier switch
        {
            CheckTier.Slow => "slow",
            CheckTier.Platform => "platform",
            _ => "fast"
        };

    private static string Skip(CheckSkipReason reason)
        => reason switch
        {
            CheckSkipReason.PlatformMismatch => "platformMismatch",
            CheckSkipReason.CiOnly => "ciOnly",
            CheckSkipReason.ToolchainMissing => "toolchainMissing",
            _ => "skipped"
        };
}
