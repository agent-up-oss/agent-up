namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Coverage of the changed lines only.
/// </summary>
/// <param name="CoveredLines">Changed, coverable lines with at least one hit.</param>
/// <param name="CoverableLines">Changed lines a coverage tool considers coverable.</param>
/// <param name="Minimum">Required percentage, from configuration.</param>
/// <param name="Uncovered">Changed, coverable, unhit lines by file.</param>
/// <param name="FilesWithoutReport">
/// Changed files that no coverage report mentions. Blocking: an unreported production file
/// usually means its test project never ran, which would otherwise read as full coverage.
/// </param>
public sealed record PatchCoverageResult(
    int CoveredLines,
    int CoverableLines,
    double Minimum,
    IReadOnlyList<UncoveredFile> Uncovered,
    IReadOnlyList<string> FilesWithoutReport)
{
    public static PatchCoverageResult NothingToCover(double minimum)
        => new(0, 0, minimum, [], []);

    /// <summary>
    /// Percentage of changed coverable lines that are covered. A change with no coverable
    /// lines scores 100: documentation and configuration edits must not fail a gate they
    /// cannot satisfy.
    /// </summary>
    public double Percentage => CoverableLines == 0 ? 100d : 100d * CoveredLines / CoverableLines;

    public bool Satisfied => FilesWithoutReport.Count == 0 && Percentage >= Minimum;
}
