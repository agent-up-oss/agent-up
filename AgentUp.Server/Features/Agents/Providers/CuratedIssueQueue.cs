using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;

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
    public async Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken)
    {
        // A task under review is still this author's, but there is nothing to do until a reviewer
        // asks for changes or the host's checks fail on it, so it waits rather than starting
        // something else.
        var held = queue.Held(workspaceId);
        if (held is { Stage: TaskStage.InReview })
            held = await ReadChecksAsync(workspaceId, held, cancellationToken);
        if (held is not null)
            return held.Stage is TaskStage.InReview ? [] : [Issue(held)];

        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return [];

        return
        [
            .. queue.List()
                .Where(task => task.HeldByWorkspaceId is null
                    && task.Stage == TaskStage.Queued
                    && task.OfferedTo(workspaceId, repository.Owner, repository.Repository))
                .Select(Issue)
        ];
    }

    /// <summary>
    /// A failed check on the revision under review is feedback like a request for changes, so it
    /// sends the task back to its author. Only the latest result of each check counts, and only on
    /// the commit the revision is; a host that cannot be read leaves the task where it was.
    /// </summary>
    private async Task<QueuedTask> ReadChecksAsync(string workspaceId, QueuedTask held, CancellationToken cancellationToken)
    {
        if (held.Proposal?.Head is not { } head)
            return held;
        ForgeTaskFeedbackDto? feedback;
        try
        {
            feedback = await forge.ReadFeedbackAsync(workspaceId, held.Proposal.Branch, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return held;
        }

        if (feedback is null || !string.Equals(feedback.HeadSha, head, StringComparison.Ordinal) || !TaskChecks.Failing(feedback.Checks).Any())
            return held;
        return queue.ChecksFailed(held.Owner, held.Repository, held.Id, head) ?? held;
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
