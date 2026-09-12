using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Services;

namespace AgentUp.Desktop.Features.Git.Controllers;

public sealed class GitController
{
    private readonly GitChangeListService _changes;

    public GitController(GitChangeListService changes)
    {
        _changes = changes;
    }

    public async Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _changes.GetChangesAsync(workspaceId, cancellationToken);

    public async Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => await _changes.GetFileDiffAsync(workspaceId, path, cancellationToken);

    public async Task<GitCommitResultDto> CommitAsync(
        string workspaceId,
        IReadOnlyList<string> files,
        string message,
        CancellationToken cancellationToken = default)
        => await _changes.CommitAsync(workspaceId, files, message, cancellationToken);

    public Task<GitMutationResultDto> DiscardAsync(
        string workspaceId,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default)
        => _changes.DiscardAsync(workspaceId, files, cancellationToken);

    public Task<GitMutationResultDto> SwitchBranchAsync(
        string workspaceId,
        string name,
        bool create,
        CancellationToken cancellationToken = default)
        => _changes.SwitchBranchAsync(workspaceId, name, create, cancellationToken);
}
