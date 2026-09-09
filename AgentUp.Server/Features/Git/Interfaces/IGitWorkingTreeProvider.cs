using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Git.Interfaces;

public interface IGitWorkingTreeProvider
{
    Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<GitFileDiff?> GetFileDiffAsync(string worktreePath, string filePath, CancellationToken cancellationToken = default);

    Task<string> CommitAsync(string worktreePath, IReadOnlyList<string> files, string message, CancellationToken cancellationToken = default);
}
