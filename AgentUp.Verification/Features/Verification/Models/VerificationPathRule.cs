namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// A static rule mapping changed paths to the checks they require. Rules are evaluated in
/// order and every match contributes, so a file may require checks from several rules.
/// </summary>
/// <param name="Match">Glob matched against repo-relative, forward-slashed paths.</param>
/// <param name="Checks">
/// Check ids required by a matching file. An empty list is an explicit "nothing required",
/// which is how documentation paths opt out without leaving the map incomplete.
/// </param>
public sealed record VerificationPathRule(
    string Match,
    IReadOnlyList<string> Checks);
