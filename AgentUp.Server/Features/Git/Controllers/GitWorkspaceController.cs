using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;

namespace AgentUp.Server.Features.Git.Controllers;

/// <summary>
/// Inbound Git write boundary for sibling slices: the commits and checkouts the Server makes around
/// an autonomous team member's work. HTTP stays on <see cref="GitChangesController"/>.
/// </summary>
public sealed class GitWorkspaceController(GitChangeTreeService changes)
{
    public Task<GitCommitResult> CommitAllToBranchAsync(
        string workspaceId,
        string branch,
        string message,
        GitIdentity author,
        CancellationToken cancellationToken = default)
        => changes.CommitAllToBranchAsync(workspaceId, branch, message, author, cancellationToken);

    public Task<GitMutationResult> StartBranchAsync(string workspaceId, string branch, CancellationToken cancellationToken = default)
        => changes.StartBranchAsync(workspaceId, branch, cancellationToken);

    public Task<GitMutationResult> ResetToRemoteBranchAsync(string workspaceId, string branch, CancellationToken cancellationToken = default)
        => changes.ResetToRemoteBranchAsync(workspaceId, branch, cancellationToken);
}
