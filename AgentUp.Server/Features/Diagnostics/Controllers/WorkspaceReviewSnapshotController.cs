using AgentUp.Server.Features.Diagnostics.Services;

namespace AgentUp.Server.Features.Diagnostics.Controllers;

public sealed class WorkspaceReviewSnapshotController(WorkspaceReviewSnapshotService snapshots)
{
    public Task<string?> GetAsync(string workspaceId, CancellationToken cancellationToken)
        => snapshots.GetAsync(workspaceId, cancellationToken);
}
