using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.TaskSource.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Turns one of a workspace's open issues into a queued task. Picking goes through the host rather
/// than trusting the caller: a task has to be a real open issue on the repository the workspace is
/// actually checked out on, or an agent would be sent after something that does not exist.
/// </summary>
public sealed class TaskPickingService(
    TaskQueueService queue,
    ForgeController forge,
    TaskSourceController source)
{
    /// <summary>
    /// Picks one issue into the queue, from wherever the request says it comes from.
    ///
    /// Naming a workspace picks from that workspace's own repository, which is how a Server with
    /// no task source configured has always worked. Leaving it out picks from the Server's task
    /// source: one backlog for the whole team, rather than one per workspace.
    /// </summary>
    public Task<QueuedTask?> PickAsync(EnqueueTaskRequest request, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(request.WorkspaceId)
            ? PickFromSourceAsync(request.IssueId, request.TargetWorkspaceId, cancellationToken)
            : PickAsync(request.WorkspaceId, request.IssueId, request.TargetWorkspaceId, cancellationToken);

    /// <summary>
    /// Picks from the Server's own task source rather than from a workspace.
    ///
    /// This is where work comes from once a Server has been pointed at a repository: one backlog
    /// for the whole team, instead of whichever repository the reader's open workspace happened
    /// to be checked out on. Returns null when the source serves no open issue with that id,
    /// which is also the answer when nothing is configured or signed in.
    /// </summary>
    public async Task<QueuedTask?> PickFromSourceAsync(
        string issueId,
        string? targetWorkspaceId,
        CancellationToken cancellationToken = default)
    {
        var configured = source.Describe();
        if (!configured.Configured)
            return null;

        var issues = await source.ListIssuesAsync(cancellationToken);
        var issue = issues.FirstOrDefault(item => item.Id == issueId);
        if (issue is null)
            return null;

        return queue.Enqueue(new QueuedTask(
            issue.Id,
            configured.Owner!,
            configured.Repository!,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.Url,
            DateTimeOffset.UtcNow,
            targetWorkspaceId));
    }

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
