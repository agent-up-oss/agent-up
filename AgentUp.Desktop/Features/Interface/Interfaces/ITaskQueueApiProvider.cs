using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>The Server routes behind the Tasks tab's queue.</summary>
public interface ITaskQueueApiProvider
{
    Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken);

    /// <summary>The open issues of one repository the stored token can read.</summary>
    Task<IReadOnlyList<OfferedIssue>> ListRepositoryIssuesAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken);

    Task EnqueueAsync(
        string? workspaceId,
        string issueId,
        string? targetWorkspaceId,
        CancellationToken cancellationToken,
        string? moduleId = null,
        string? owner = null,
        string? repository = null);

    Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken);

    Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken);

    Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken);

    Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken);
}
