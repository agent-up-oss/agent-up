using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Commits.Services;

public sealed class WorkspaceCommitQueueService(WorkspaceQueryController workspaces, CommitsController commits, GitQueryController git)
{
    public async Task<WorkspaceCommitQueueResult> GetAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return WorkspaceCommitQueueResult.Missing();

        // A folder that is not a repository is a valid workspace with nothing queued, not a fault.
        var head = await git.GetHeadAsync(workspaceId, cancellationToken);
        if (head is { IsRepository: false })
            return WorkspaceCommitQueueResult.Success(new CommitsStatusResult([], []));

        var queue = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
        return WorkspaceCommitQueueResult.Success(queue);
    }
}
