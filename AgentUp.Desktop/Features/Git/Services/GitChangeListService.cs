using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Interfaces;

namespace AgentUp.Desktop.Features.Git.Services;

public sealed class GitChangeListService
{
    private readonly IGitApiProvider _client;

    public GitChangeListService(IGitApiProvider client)
    {
        _client = client;
    }

    public async Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.GetChangesAsync(workspaceId, cancellationToken);

    public async Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => await _client.GetFileDiffAsync(workspaceId, path, cancellationToken);

    public async Task<GitCommitResultDto> CommitAsync(
        string workspaceId,
        IReadOnlyList<string> files,
        string message,
        CancellationToken cancellationToken = default)
        => await _client.CommitAsync(workspaceId, new GitCommitRequestDto(files, message), cancellationToken);

    public Task<GitMutationResultDto> DiscardAsync(
        string workspaceId,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default)
        => _client.DiscardAsync(workspaceId, new GitFilesRequestDto(files), cancellationToken);

    public Task<GitMutationResultDto> SwitchBranchAsync(
        string workspaceId,
        string name,
        bool create,
        CancellationToken cancellationToken = default)
        => _client.SwitchBranchAsync(workspaceId, new GitBranchRequestDto(name, create), cancellationToken);
}
