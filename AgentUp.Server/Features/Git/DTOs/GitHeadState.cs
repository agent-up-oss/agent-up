namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitHeadState(
    string Branch,
    IReadOnlyList<string> LocalBranches,
    IReadOnlyList<GitRemoteBranch> RemoteBranches,
    string? Upstream,
    int Ahead,
    int Behind,
    string Commit)
{
    public static GitHeadState Empty(string branch, string commit = "") =>
        new(branch, [], [], null, 0, 0, commit);
}
