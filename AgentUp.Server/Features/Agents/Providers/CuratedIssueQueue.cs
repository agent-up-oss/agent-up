using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// What an autonomous workspace is allowed to work on: the tasks somebody picked into the queue,
/// never everything the host has open.
///
/// A workspace is offered its own held task first and nothing else, because a hold is sticky and
/// the task it started is the task it owes. Only when it holds nothing does it see the free tasks
/// for its own repository that are either untargeted or pointed at it.
///
/// The queue's order is the order the user arranged, so the per-workspace sort is not applied
/// here: an explicitly arranged queue would be meaningless if the loop re-sorted it.
/// </summary>
public sealed class CuratedIssueQueue(TaskQueueService queue, ForgeController forge) : IAssignedIssueQueue
{
    public Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken)
    {
        var held = queue.Held(workspaceId);
        if (held is not null)
            return Task.FromResult<IReadOnlyList<AssignedIssue>>([Issue(held)]);

        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return Task.FromResult<IReadOnlyList<AssignedIssue>>([]);

        return Task.FromResult<IReadOnlyList<AssignedIssue>>(
        [
            .. queue.List()
                .Where(task => task.HeldByWorkspaceId is null
                    && task.OfferedTo(workspaceId, repository.Owner, repository.Repository))
                .Select(Issue)
        ]);
    }

    public Task<bool> TryHoldAsync(string workspaceId, string issueId, CancellationToken cancellationToken)
    {
        var repository = forge.ResolveRepository(workspaceId);
        return Task.FromResult(repository is not null
            && queue.TryClaim(workspaceId, repository.Owner, repository.Repository, issueId));
    }

    public Task CompleteAsync(string workspaceId, string issueId, CancellationToken cancellationToken)
    {
        queue.Complete(workspaceId, issueId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Nothing to do: the queue is the record, and a run that did not finish keeps its hold so the
    /// same workspace picks the task back up.
    /// </summary>
    public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
    {
    }

    private static AssignedIssue Issue(QueuedTask task) => new(
        task.Id,
        task.Number,
        task.Title,
        task.Body,
        task.QueuedAt,
        task.QueuedAt,
        task.Url);
}
