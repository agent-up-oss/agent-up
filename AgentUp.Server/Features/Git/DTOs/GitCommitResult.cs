namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitCommitResult(bool Found, bool Succeeded, string? Commit, string? Error)
{
    public static GitCommitResult NotFound() => new(false, false, null, null);

    public static GitCommitResult Success(string commit) => new(true, true, commit, null);

    public static GitCommitResult Failed(string error) => new(true, false, null, error);
}
