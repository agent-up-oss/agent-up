using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;

namespace AgentUp.Server.Features.Git.Controllers;

/// <summary>Inbound Git read boundary for sibling slices. HTTP stays on <see cref="GitChangesController"/>.</summary>
public sealed class GitQueryController(GitChangeTreeService changes)
{
    public Task<GitChangeTree?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
        => changes.GetChangesAsync(workspaceId, cancellationToken);

    public Task<GitHeadState?> GetHeadAsync(string workspaceId, CancellationToken cancellationToken = default)
        => changes.GetHeadAsync(workspaceId, cancellationToken);
}
