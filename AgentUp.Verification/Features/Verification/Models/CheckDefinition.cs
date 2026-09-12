namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// One named, reusable unit of proof. Defined once in agent-up.json and referenced by id
/// from path rules, so the same command cannot drift between callers.
/// </summary>
/// <param name="Id">Stable identifier referenced by path rules.</param>
/// <param name="Command">Shell command to execute from <paramref name="WorkingDirectory"/>.</param>
/// <param name="WorkingDirectory">Repo-relative directory, or null for the repository root.</param>
/// <param name="Tier">Cost class governing how broadly this check may be required.</param>
/// <param name="Platforms">
/// Platform identifiers ("linux", "macos", "windows") this check can run on.
/// Empty means every platform.
/// </param>
/// <param name="CiOnly">When true the check is required in CI but never blocks a local run.</param>
/// <param name="Order">
/// Sort key among selected checks, ascending, ties broken by id. Lets a check that
/// consumes another check's output run after it - patch coverage must follow the suites
/// that write the coverage reports, which id order alone would not guarantee.
/// </param>
/// <param name="Inputs">
/// Repo-relative path prefixes whose changed files belong in this check's covered map —
/// the check's dependency closure. Empty means the check covers nothing by itself and
/// relies entirely on the path rules that select it.
/// </param>
public sealed record CheckDefinition(
    string Id,
    string Command,
    string? WorkingDirectory,
    CheckTier Tier,
    IReadOnlyList<string> Platforms,
    bool CiOnly,
    int Order,
    IReadOnlyList<string> Inputs);
