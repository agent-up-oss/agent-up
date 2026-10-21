namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>Shared plan transcript. Guard and run transcripts stay with each host.</summary>
public static class VerificationConsoleText
{
    public static void WritePlan(TextWriter output, VerificationPlan plan)
    {
        if (plan.ChangedFiles.Count == 0)
        {
            output.WriteLine("Nothing changed, so no checks are required.");
            return;
        }

        output.WriteLine($"{plan.Checks.Count} check(s) required by {plan.ChangedFiles.Count} changed file(s):");
        foreach (var check in plan.Checks)
            output.WriteLine(VerificationPlanText.Format(check));

        WriteUnmatched(output, plan.UnmatchedFiles);
    }

    public static void WriteUnmatched(TextWriter writer, IReadOnlyList<string> unmatched, string prefix = "  ")
    {
        if (unmatched.Count == 0)
            return;

        writer.WriteLine($"{prefix}{unmatched.Count} changed file(s) match no path rule, so the verification map is incomplete:");
        foreach (var path in unmatched)
            writer.WriteLine($"{prefix}  {path}");
    }
}
