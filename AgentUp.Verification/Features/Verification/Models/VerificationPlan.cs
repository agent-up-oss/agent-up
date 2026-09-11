namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// What the current change set requires. Produced only from the static rules and the
/// changed-file set, never from caller input, so the set cannot be narrowed by an agent.
/// </summary>
/// <param name="Checks">Required checks in stable id order.</param>
/// <param name="ChangedFiles">Repo-relative path to content hash for every changed file.</param>
/// <param name="UnmatchedFiles">
/// Changed files no path rule matched. Non-empty means the map is incomplete: the caller
/// treats this as a hard failure rather than a silent pass.
/// </param>
public sealed record VerificationPlan(
    IReadOnlyList<PlannedCheck> Checks,
    IReadOnlyDictionary<string, string> ChangedFiles,
    IReadOnlyList<string> UnmatchedFiles)
{
    public static readonly VerificationPlan Nothing = new(
        [],
        new Dictionary<string, string>(StringComparer.Ordinal),
        []);

    public bool HasWork => Checks.Count > 0;
}
