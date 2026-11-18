namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitHeadState(
    string Branch,
    IReadOnlyList<string> LocalBranches,
    IReadOnlyList<GitRemoteBranch> RemoteBranches,
    string? Upstream,
    int Ahead,
    int Behind,
    string Commit,
    bool IsRepository = true)
{
    /// <summary>
    /// A folder Git could not read as a repository. The registered branch and commit stand in for
    /// display only, so callers ask <see cref="IsRepository"/> before treating them as Git state.
    /// </summary>
    public static GitHeadState Empty(string branch, string commit = "") =>
        new(branch, [], [], null, 0, 0, commit, IsRepository: false);
}
