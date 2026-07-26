namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsGitProvider
{
    Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default);
    Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default);
}
