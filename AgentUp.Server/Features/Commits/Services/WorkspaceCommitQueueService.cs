using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Commits.Services;

public sealed class WorkspaceCommitQueueService(WorkspaceQueryController workspaces, CommitsController commits)
{
    public async Task<WorkspaceCommitQueueResult> GetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return WorkspaceCommitQueueResult.Missing();

        var queue = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
        return WorkspaceCommitQueueResult.Success(queue);
    }
}
