namespace AgentUp.Server.Features.Commits.Interfaces;

public interface ICommitsGitProvider
{
    Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default);
    Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default);
}
