namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The static verification rules for one repository, read from the "verification" section
/// of agent-up.json.
/// </summary>
/// <param name="Enforcement">Whether an unsatisfied guard reports or blocks.</param>
/// <param name="Always">Check ids required whenever anything at all changed.</param>
/// <param name="Checks">Check definitions by id.</param>
/// <param name="Paths">Path rules in evaluation order.</param>
public sealed record VerificationConfiguration(
    VerificationEnforcement Enforcement,
    IReadOnlyList<string> Always,
    IReadOnlyDictionary<string, CheckDefinition> Checks,
    IReadOnlyList<VerificationPathRule> Paths)
{
    /// <summary>
    /// A repository with no verification section. Distinguished from a broken one: the
    /// loader throws rather than returning this when the section exists but is invalid.
    /// </summary>
    public static readonly VerificationConfiguration Empty = new(
        VerificationEnforcement.Warn,
        [],
        new Dictionary<string, CheckDefinition>(StringComparer.Ordinal),
        []);

    public bool IsConfigured => Checks.Count > 0;
}
