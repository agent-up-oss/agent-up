using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Git.Interfaces;

public interface IGitWorkingTreeProvider
{
    Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<GitHeadState> GetHeadStateAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<GitFileDiff?> GetFileDiffAsync(string worktreePath, string filePath, CancellationToken cancellationToken = default);

    Task<string> CommitAsync(string worktreePath, IReadOnlyList<string> files, string message, CancellationToken cancellationToken = default);

    Task DiscardAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default);

    Task SwitchBranchAsync(string worktreePath, string name, bool create, CancellationToken cancellationToken = default);

    Task CheckoutRemoteAsync(string worktreePath, string name, CancellationToken cancellationToken = default);

    Task FetchAsync(string worktreePath, string? remote, CancellationToken cancellationToken = default);

    Task PullAsync(string worktreePath, bool rebase, CancellationToken cancellationToken = default);

    Task PushAsync(string worktreePath, bool forceWithLease, bool setUpstream, CancellationToken cancellationToken = default);

    Task<GitLog> GetLogAsync(string worktreePath, int? max, CancellationToken cancellationToken = default);
}
