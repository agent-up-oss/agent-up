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
public sealed record CoverageConfiguration(
    double Minimum,
    string ReportDirectory,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude)
{
    public static readonly CoverageConfiguration Empty = new(0d, "artifacts/coverage", [], []);

    public bool IsConfigured => Include.Count > 0;
}
