using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAssignedProposalQueue
{
    Task<IReadOnlyList<AssignedProposal>> ListAwaitingAsync(string workspaceId, CancellationToken cancellationToken);

    void Replace(string workspaceId, IReadOnlyList<AssignedProposal> proposals);
}
