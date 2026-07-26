namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsGitProvider
{
    Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default);
    Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
    Task ResetStagingAsync(CancellationToken cancellationToken = default);
}
