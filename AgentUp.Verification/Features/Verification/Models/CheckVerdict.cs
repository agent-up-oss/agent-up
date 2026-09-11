namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The guard's finding for one required check, with enough detail for an agent to act
/// without reading any other output.
/// </summary>
public sealed record CheckVerdict(
    string CheckId,
    string Command,
    CheckVerdictKind Kind,
    string Detail)
{
    /// <summary>
    /// Whether this verdict stands in the way. Skipped checks do not: they are reported
    /// with their reason and remain required wherever they can actually run.
    /// </summary>
    public bool Blocking => Kind is CheckVerdictKind.NeverRun or CheckVerdictKind.Failed or CheckVerdictKind.Stale;
}
