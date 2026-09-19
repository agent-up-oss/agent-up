namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitSyncResult(bool Found, bool Succeeded, string? Error, GitHeadState? Head)
{
    public static GitSyncResult NotFound() => new(false, false, null, null);

    public static GitSyncResult Success(GitHeadState head) => new(true, true, null, head);

    public static GitSyncResult Failed(string error) => new(true, false, error, null);
}
