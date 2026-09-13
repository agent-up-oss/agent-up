namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// The "coverage" section of agent-up.json.
/// </summary>
/// <param name="Minimum">Required percentage of changed coverable lines.</param>
/// <param name="ReportDirectory">Repo-relative root searched for Cobertura reports.</param>
/// <param name="Include">
/// Globs whose changed files are subject to the gate. Empty means every changed file,
/// which is almost never what a repository wants.
/// </param>
/// <param name="Exclude">
/// Globs removed from the measurement after Include is applied — generated code,
/// composition roots, and other files where a coverage number carries no information.
/// </param>
/// <param name="SliceMinimum">
/// Required total line coverage for each feature slice. Lower than <paramref name="Minimum"/>
/// by design: patch coverage governs new work, while this is a floor under what already
/// exists, and raising it is a decision to burn the remaining debt down.
/// </param>
/// <param name="SliceExemptions">
/// Slices, as "&lt;Project&gt;/Features/&lt;Slice&gt;", allowed below SliceMinimum. Each is
/// accepted debt: the check fails once a listed slice reaches the floor, so the list cannot
/// outlive what it records.
/// </param>
public sealed record CoverageConfiguration(
    double Minimum,
    string ReportDirectory,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    double SliceMinimum,
    IReadOnlyList<string> SliceExemptions)
{
    public static readonly CoverageConfiguration Empty =
        new(0d, "artifacts/coverage", [], [], 0d, []);

    public bool IsConfigured => Include.Count > 0;
}
