namespace AgentUp.CLI.Features.Commits.Interfaces;

using AgentUp.CLI.Features.Commits.Models;

public interface ICommitsGitProvider
{
    Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default);
    Task<GitOperationState> GetOperationStateAsync(CancellationToken cancellationToken = default);
    Task ApplyPatchAsync(string patch, CancellationToken cancellationToken = default);
    Task RestoreFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task ResetStagingAsync(CancellationToken cancellationToken = default);
}
