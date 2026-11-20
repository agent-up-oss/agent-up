using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// What a reviewer is offered: the picked tasks on its own repository whose open revision it has
/// not reviewed. The queue is the record, so a reviewer never reviews a proposal nobody picked,
/// and a new revision puts the task back in front of every reviewer.
/// </summary>
public sealed class CuratedProposalQueue(TaskQueueService queue, ForgeController forge) : IAssignedProposalQueue
{
    public Task<IReadOnlyList<AssignedProposal>> ListAwaitingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var repository = forge.ResolveRepository(workspaceId);
        if (repository is null)
            return Task.FromResult<IReadOnlyList<AssignedProposal>>([]);

        return Task.FromResult<IReadOnlyList<AssignedProposal>>(
        [
            .. queue.AwaitingReview(workspaceId, repository.Owner, repository.Repository)
                .Select(task => new AssignedProposal(task.Id, task.Title, task.Body, task.QueuedAt, task.Proposal!.Url, task.Number))
        ]);
    }

    /// <summary>Nothing to do: a review recorded on the task is what takes it off this list.</summary>
    public void Replace(string workspaceId, IReadOnlyList<AssignedProposal> proposals)
    {
    }
}
