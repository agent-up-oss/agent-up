using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAssignedProposalQueue
{
    Task<IReadOnlyList<AssignedProposal>> ListAwaitingAsync(string workspaceId, CancellationToken cancellationToken);

    void Replace(string workspaceId, IReadOnlyList<AssignedProposal> proposals);

    /// <summary>
    /// Takes the review of one proposal for this reviewer. False means another reviewer of the
    /// same kind holds it, and the caller should choose again.
    /// </summary>
    Task<bool> TryTakeAsync(string workspaceId, string id, CancellationToken cancellationToken);
}
