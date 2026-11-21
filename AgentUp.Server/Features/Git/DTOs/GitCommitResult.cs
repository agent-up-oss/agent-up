namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitCommitResult(bool Found, bool Succeeded, string? Commit, string? Error, bool NothingToCommit = false)
{
    /// <summary>The worktree had no changes, which is a finding about the work rather than a Git failure.</summary>
    public static GitCommitResult Unchanged() => new(true, false, null, "There were no changes to commit.", true);

    public static GitCommitResult NotFound() => new(false, false, null, null);

    public static GitCommitResult Success(string commit) => new(true, true, commit, null);

    public static GitCommitResult Failed(string error) => new(true, false, null, error);
}
