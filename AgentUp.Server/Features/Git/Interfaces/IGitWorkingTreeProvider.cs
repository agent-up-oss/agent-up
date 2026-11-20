using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Git.Interfaces;

public interface IGitWorkingTreeProvider
{
    Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<GitHeadState> GetHeadStateAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<GitFileDiff?> GetFileDiffAsync(string worktreePath, string filePath, CancellationToken cancellationToken = default);

    Task<string> CommitAsync(string worktreePath, IReadOnlyList<string> files, string message, CancellationToken cancellationToken = default);

    Task DiscardAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits every change in the worktree onto <paramref name="branch"/>, creating it from HEAD
    /// when it does not exist, under the given author. Returns the new commit, or null when the
    /// worktree had nothing to commit.
    /// </summary>
    Task<string?> CommitAllToBranchAsync(string worktreePath, string branch, string message, GitIdentity author, CancellationToken cancellationToken = default);

    /// <summary>The unified diff of what <paramref name="head"/> adds since it left <paramref name="baseRef"/>.</summary>
    Task<string> GetRangeDiffAsync(string worktreePath, string baseRef, string head, CancellationToken cancellationToken = default);

    /// <summary>The commit <paramref name="reference"/> names, or null when the repository has no such ref.</summary>
    Task<string?> ResolveCommitAsync(string worktreePath, string reference, CancellationToken cancellationToken = default);

    /// <summary>Points <paramref name="branch"/> at the fetched <c>origin/&lt;branch&gt;</c> and checks it out.</summary>
    Task ResetToRemoteBranchAsync(string worktreePath, string branch, CancellationToken cancellationToken = default);

    Task SwitchBranchAsync(string worktreePath, string name, bool create, CancellationToken cancellationToken = default);

    Task CheckoutRemoteAsync(string worktreePath, string name, CancellationToken cancellationToken = default);

    Task FetchAsync(string worktreePath, string? remote, CancellationToken cancellationToken = default);

    Task PullAsync(string worktreePath, bool rebase, CancellationToken cancellationToken = default);

    Task PushAsync(string worktreePath, bool forceWithLease, bool setUpstream, CancellationToken cancellationToken = default);

    Task<GitLog> GetLogAsync(
        string worktreePath,
        int? max,
        CancellationToken cancellationToken = default,
        int? skip = null,
        string? until = null);
}
