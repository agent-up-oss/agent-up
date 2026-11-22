using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;

namespace AgentUp.Server.Features.Git.Controllers;

/// <summary>Inbound Git read boundary for sibling slices. HTTP stays on <see cref="GitChangesController"/>.</summary>
public sealed class GitQueryController(GitChangeTreeService changes)
{
    public Task<GitChangeTree?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => changes.GetChangesAsync(workspaceId, cancellationToken);

    /// <summary>Changed files relative to the workspace folder, for slices that show that folder's own files.</summary>
    public Task<IReadOnlyList<GitChangeEntry>?> GetWorktreeChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => changes.GetWorktreeChangesAsync(workspaceId, cancellationToken);

    public Task<string?> GetDiffAsync(string workspaceId, string baseRef, string head, CancellationToken cancellationToken = default)
        => changes.GetRangeDiffAsync(workspaceId, baseRef, head, cancellationToken);

    /// <summary>The commit a ref names, such as a fetched <c>refs/remotes/origin/&lt;branch&gt;</c>.</summary>
    public Task<string?> ResolveCommitAsync(string workspaceId, string reference, CancellationToken cancellationToken = default)
        => changes.ResolveCommitAsync(workspaceId, reference, cancellationToken);

    public Task<GitHeadState?> GetHeadAsync(string workspaceId, CancellationToken cancellationToken = default)
        => changes.GetHeadAsync(workspaceId, cancellationToken);
}
