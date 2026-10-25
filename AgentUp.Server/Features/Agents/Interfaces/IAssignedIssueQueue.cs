using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAssignedIssueQueue
{
    Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken);

    void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues);

    /// <summary>
    /// Takes the issue for this workspace before any work starts on it. False means another
    /// workspace holds it now and this one must choose again, which is how one shared queue can
    /// feed many workspaces without two of them opening the same issue.
    /// </summary>
    Task<bool> TryHoldAsync(string workspaceId, string issueId, CancellationToken cancellationToken);

    /// <summary>Retires a finished issue so nothing picks it up again.</summary>
    Task CompleteAsync(string workspaceId, string issueId, CancellationToken cancellationToken);
}
