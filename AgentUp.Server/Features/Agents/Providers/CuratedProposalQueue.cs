using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Git.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// What a reviewer is offered: the picked tasks on its own repository whose open revision it has
/// not reviewed. The queue is the record, so a reviewer never reviews a proposal nobody picked.
/// A review is triggered by a push: before answering, the reviewer fetches every proposed branch
/// with its own credential, and a branch whose head moved - whoever pushed it - is a new revision
/// in front of every reviewer.
/// </summary>
public sealed class CuratedProposalQueue(TaskQueueService queue, ForgeController forge, GitQueryController? git = null) : IAssignedProposalQueue
{
    public async Task<IReadOnlyList<AssignedProposal>> ListAwaitingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return [];

        await ReadPushesAsync(workspaceId, queue.Proposed(repository.Owner, repository.Repository), cancellationToken);
        return
        [
            .. queue.AwaitingReview(workspaceId, repository.Owner, repository.Repository)
                .Select(task => new AssignedProposal(task.Id, task.Title, task.Body, task.QueuedAt, task.Proposal!.Url, task.Number))
        ];
    }

    /// <summary>Nothing to do: a review recorded on the task is what takes it off this list.</summary>
    public void Replace(string workspaceId, IReadOnlyList<AssignedProposal> proposals)
    {
    }

    /// <summary>
    /// Brings each proposed branch's head in from the host, one branch at a time so a branch the
    /// host deleted after a merge does not hide a push to another. A fetch the host refuses leaves
    /// that task as it was: the revision already recorded is still the one to review.
    /// </summary>
    private async Task ReadPushesAsync(string workspaceId, IReadOnlyList<QueuedTask> proposed, CancellationToken cancellationToken)
    {
        if (git is null)
            return;
        foreach (var task in proposed)
        {
            var fetched = await forge.FetchBranchesAsync(workspaceId, [task.Proposal!.Branch], cancellationToken);
            if (!fetched.Succeeded)
                continue;
            var head = await git.ResolveCommitAsync(workspaceId, $"refs/remotes/origin/{task.Proposal.Branch}", cancellationToken);
            if (head is not null)
                queue.Pushed(task.Owner, task.Repository, task.Id, head);
        }
    }
}
