namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Line coverage of one feature slice, identified as "&lt;Project&gt;/Features/&lt;Slice&gt;".
/// </summary>
public sealed record SliceCoverage(string Slice, int Covered, int Coverable)
{
    public double Percent => Coverable == 0 ? 100d : 100d * Covered / Coverable;

    public bool Meets(double minimum) => Percent >= minimum;

    public SliceCoverage Add(int covered, int coverable)
        => this with { Covered = Covered + covered, Coverable = Coverable + coverable };
}
