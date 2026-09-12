namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitMutationResult(bool Found, bool Succeeded, string? Error)
{
    public static GitMutationResult NotFound() => new(false, false, null);

    public static GitMutationResult Success() => new(true, true, null);

    public static GitMutationResult Failed(string error) => new(true, false, error);
}
