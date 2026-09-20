using AgentUp.Desktop.Features.Git.DTOs;

namespace AgentUp.Desktop.Features.Git.Interfaces;

public interface IGitApiProvider
{
    Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<CommitQueueDto?> GetCommitQueueAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default);

    Task<GitCommitResultDto> CommitAsync(string workspaceId, GitCommitRequestDto request, CancellationToken cancellationToken = default);

    Task<GitMutationResultDto> DiscardAsync(string workspaceId, GitFilesRequestDto request, CancellationToken cancellationToken = default);

    Task<GitMutationResultDto> SwitchBranchAsync(string workspaceId, GitBranchRequestDto request, CancellationToken cancellationToken = default);

    Task<GitMutationResultDto> CheckoutRemoteAsync(string workspaceId, GitCheckoutRequestDto request, CancellationToken cancellationToken = default);

    Task<GitSyncResultDto> FetchAsync(string workspaceId, GitFetchRequestDto request, CancellationToken cancellationToken = default);

    Task<GitSyncResultDto> PullAsync(string workspaceId, GitPullRequestDto request, CancellationToken cancellationToken = default);

    Task<GitSyncResultDto> PushAsync(string workspaceId, GitPushRequestDto request, CancellationToken cancellationToken = default);

    Task<GitLogDto?> GetLogAsync(string workspaceId, int max = 100, CancellationToken cancellationToken = default);
}
