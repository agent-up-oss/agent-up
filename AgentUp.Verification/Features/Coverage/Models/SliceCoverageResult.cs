namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Per-slice coverage measured against a floor, with the slices exempted from it.
/// </summary>
/// <param name="Slices">Every measured slice, worst first.</param>
/// <param name="Minimum">The floor each slice has to reach.</param>
/// <param name="Failing">Slices below the floor and not exempt.</param>
/// <param name="ResolvedExemptions">
/// Slices listed as exempt that now reach the floor. These fail the check: an exemption
/// that outlives the debt it records turns the floor into a suggestion.
/// </param>
public sealed record SliceCoverageResult(
    IReadOnlyList<SliceCoverage> Slices,
    double Minimum,
    IReadOnlyList<SliceCoverage> Failing,
    IReadOnlyList<string> ResolvedExemptions)
{
    public static SliceCoverageResult NothingMeasured(double minimum) => new([], minimum, [], []);

    public bool IsSatisfied => Failing.Count == 0 && ResolvedExemptions.Count == 0;

    /// <summary>True when no report mentioned any slice, so nothing was measured at all.</summary>
    public bool MeasuredNothing => Slices.Count == 0;
}
