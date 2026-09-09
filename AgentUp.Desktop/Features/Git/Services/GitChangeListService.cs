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
}
