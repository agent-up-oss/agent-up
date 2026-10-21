namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Exit codes for the portable runner. A missing toolchain is not a failed check, and an
/// incomplete path map is not either of those.
/// </summary>
public static class AuCheckExit
{
    public const int Passed = 0;
    public const int Failed = 1;
    public const int CannotRun = 2;
    public const int Invalid = 3;

    public const string ReasonPassed = "passed";
    public const string ReasonFailed = "failed";
    public const string ReasonCannotRun = "toolchainMissing";
    public const string ReasonUnmatched = "unmatchedPath";
    public const string ReasonInvalid = "invalidConfiguration";

    public static (int Code, string Reason) ForPlan(VerificationPlan plan)
    {
        if (plan.UnmatchedFiles.Count > 0)
            return (Invalid, ReasonUnmatched);

        if (plan.Checks.Any(check => check.SkipReason == CheckSkipReason.ToolchainMissing))
            return (CannotRun, ReasonCannotRun);

        return (Passed, ReasonPassed);
    }

    public static (int Code, string Reason) ForRun(VerificationPlan plan, IReadOnlyList<CheckOutcome> outcomes)
    {
        if (plan.UnmatchedFiles.Count > 0)
            return (Invalid, ReasonUnmatched);

        if (outcomes.Any(outcome => !outcome.Succeeded))
            return (Failed, ReasonFailed);

        if (plan.Checks.Any(check => check.SkipReason == CheckSkipReason.ToolchainMissing))
            return (CannotRun, ReasonCannotRun);

        return (Passed, ReasonPassed);
    }

    public static (int Code, string Reason) ForGuard(GuardReport report)
    {
        if (report.UnmatchedFiles.Count > 0)
            return (Invalid, ReasonUnmatched);

        if (!report.ShouldBlock)
            return (Passed, ReasonPassed);

        if (report.Verdicts.Any(verdict => verdict.Kind == CheckVerdictKind.Failed))
            return (Failed, ReasonFailed);

        if (report.Verdicts.Any(verdict => verdict.SkipReason == CheckSkipReason.ToolchainMissing))
            return (CannotRun, ReasonCannotRun);

        return (Failed, ReasonFailed);
    }
}
