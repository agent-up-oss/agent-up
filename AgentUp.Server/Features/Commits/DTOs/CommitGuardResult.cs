namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitGuardResult(bool Success, IReadOnlyList<string> Blockers, string? ContinueWorktreePath = null)
{
    public static CommitGuardResult Passed(string? continueWorktreePath = null) => new(true, [], continueWorktreePath);
    public static CommitGuardResult Failed(IReadOnlyList<string> blockers) => new(false, blockers);
}
