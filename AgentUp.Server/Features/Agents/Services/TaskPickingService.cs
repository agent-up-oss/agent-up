using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Turns one of a workspace's open issues into a queued task. Picking goes through the host rather
/// than trusting the caller: a task has to be a real open issue on the repository the workspace is
/// actually checked out on, or an agent would be sent after something that does not exist.
/// </summary>
public sealed class TaskPickingService(TaskQueueService queue, ForgeController forge)
{
    /// <summary>
    /// Returns null when the workspace has no repository, or when the host serves no open issue
    /// with that id.
    /// </summary>
    public async Task<QueuedTask?> PickAsync(
        string workspaceId,
        string issueId,
        string? targetWorkspaceId,
        CancellationToken cancellationToken = default)
    {
        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return null;

        var issues = await forge.ListIssuesAsync(workspaceId, cancellationToken);
        var issue = issues.FirstOrDefault(item => item.Id == issueId);
        if (issue is null)
            return null;

        return queue.Enqueue(new QueuedTask(
            issue.Id,
            repository.Owner,
            repository.Repository,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.Url,
            DateTimeOffset.UtcNow,
            targetWorkspaceId));
    }
}
