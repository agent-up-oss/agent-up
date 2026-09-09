using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Features.Git.Interfaces;

public interface IGitApiProvider
{
    Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default);

    Task<GitCommitResultDto> CommitAsync(string workspaceId, GitCommitRequestDto request, CancellationToken cancellationToken = default);
}
