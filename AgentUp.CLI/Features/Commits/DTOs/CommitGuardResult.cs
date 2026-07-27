namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitGuardResult(bool Success, IReadOnlyList<string> Blockers)
{
    public static CommitGuardResult Passed() => new(true, []);
    public static CommitGuardResult Failed(IReadOnlyList<string> blockers) => new(false, blockers);
}
