using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>In-process proposals-awaiting-review queue used until a forge module is enabled.</summary>
public sealed class MemoryAssignedProposalQueue : IAssignedProposalQueue
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<AssignedProposal>> _proposals = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<AssignedProposal>> ListAwaitingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var items = _proposals.GetValueOrDefault(workspaceId) ?? [];
        return Task.FromResult<IReadOnlyList<AssignedProposal>>([.. items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id, StringComparer.Ordinal)]);
    }

    public void Replace(string workspaceId, IReadOnlyList<AssignedProposal> proposals) =>
        _proposals[workspaceId] = proposals;
}
