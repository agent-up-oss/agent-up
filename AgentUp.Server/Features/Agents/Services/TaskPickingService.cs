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
    /// Naming a workspace picks from that workspace's own repository. Leaving the workspace out
    /// and naming a host, owner, and repository picks from that repository through the personal
    /// access token stored for the host.
    /// </summary>
    public Task<QueuedTask?> PickAsync(EnqueueTaskRequest request, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(request.WorkspaceId)
            ? PickFromSourceAsync(request, cancellationToken)
            : PickAsync(request.WorkspaceId, request.IssueId, request.TargetWorkspaceId, cancellationToken);

    /// <summary>
    /// Picks from a repository the stored token can read. Returns null when the request does not
    /// name the host and repository, or when that repository has no open issue with the id.
    /// </summary>
    public async Task<QueuedTask?> PickFromSourceAsync(
        EnqueueTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleId)
            || string.IsNullOrWhiteSpace(request.Owner)
            || string.IsNullOrWhiteSpace(request.Repository))
        {
            return null;
        }

        var issues = await source.ListIssuesAsync(request.ModuleId, request.Owner, request.Repository, cancellationToken);
        var issue = issues.FirstOrDefault(item => item.Id == request.IssueId);
        if (issue is null)
            return null;

        return queue.Enqueue(new QueuedTask(
            issue.Id,
            request.Owner,
            request.Repository,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.Url,
            DateTimeOffset.UtcNow,
            request.TargetWorkspaceId));
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
