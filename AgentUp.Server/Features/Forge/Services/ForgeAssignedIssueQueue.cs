using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;

namespace AgentUp.Server.Features.Forge.Services;

/// <summary>
/// The assigned-issue queue when a forge module is enabled. Without a credential or remote
/// the list is empty — the loop then has nothing to do and can say so.
/// </summary>
public sealed class ForgeIssueQueue(ForgeController forge) : IAssignedIssueQueue
{
    public async Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
        string workspaceId,
        AssignedIssueOrder order,
        CancellationToken cancellationToken)
    {
        var issues = await forge.ListAssignedAsync(workspaceId, cancellationToken);
        var mapped = issues.Select(issue => new AssignedIssue(
            issue.Id,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.Url));
        return order switch
        {
            AssignedIssueOrder.CreatedAsc => mapped.OrderBy(issue => issue.CreatedAt).ToArray(),
            AssignedIssueOrder.UpdatedAsc => mapped.OrderBy(issue => issue.UpdatedAt).ToArray(),
            _ => mapped.OrderBy(issue => issue.Number).ToArray()
        };
    }

    public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
    {
    }
}
