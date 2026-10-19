namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The plan lines both hosts print. One formatter so <c>au-check plan</c> and
/// <c>agent-up verify plan</c> cannot drift.
/// </summary>
public static class VerificationPlanText
{
    public static string Format(PlannedCheck check)
    {
        var selection = string.Join(", ", check.SelectedBy);
        if (check.Runnable)
            return $"  {check.CheckId}: {check.Definition.Command}  [{selection}]";

        var reason = check.SkipReason == CheckSkipReason.ToolchainMissing && check.Toolchain is not null
            ? $"ToolchainMissing: {check.Toolchain.Describe()}"
            : check.SkipReason.ToString();
        return $"  {check.CheckId}: skipped here ({reason}), still required in CI  [{selection}]";
    }
}
