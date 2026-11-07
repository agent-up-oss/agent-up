using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>In-process assigned-issue queue used by tests and until a forge module is enabled.</summary>
public sealed class MemoryAssignedIssueQueue : IAssignedIssueQueue
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<AssignedIssue>> _issues = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken)
    {
        var items = _issues.GetValueOrDefault(workspaceId) ?? [];
        IEnumerable<AssignedIssue> ordered = order switch
        {
            AssignedIssueOrder.CreatedAsc => items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Number),
            AssignedIssueOrder.UpdatedAsc => items.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Number),
            _ => items.OrderBy(item => item.Number)
        };
        return Task.FromResult<IReadOnlyList<AssignedIssue>>([.. ordered]);
    }

    public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues) =>
        _issues[workspaceId] = issues;

    /// <summary>Nothing contends for these issues, so the hold always succeeds.</summary>
    public Task<bool> TryHoldAsync(string workspaceId, string issueId, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <summary>Completion is recorded by the caller replacing the list.</summary>
    public Task CompleteAsync(string workspaceId, string issueId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
