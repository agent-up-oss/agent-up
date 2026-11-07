using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>The Server routes behind the Tasks tab's queue.</summary>
public interface ITaskQueueApiProvider
{
    Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken);

    Task EnqueueAsync(string workspaceId, string issueId, string? targetWorkspaceId, CancellationToken cancellationToken);

    Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken);

    Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken);

    Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken);

    Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken);
}
