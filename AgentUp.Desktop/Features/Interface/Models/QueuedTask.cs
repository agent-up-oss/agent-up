namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>
/// One task in the Server's queue. The agents work these and only these, in this order.
/// </summary>
public sealed record QueuedTask(
    string Id,
    string Owner,
    string Repository,
    int Number,
    string Title,
    string Url,
    string? TargetWorkspaceId,
    string? HeldByWorkspaceId);

/// <summary>An open issue on a workspace's repository, offered for picking.</summary>
public sealed record OfferedIssue(string Id, int Number, string Title, string Url);
