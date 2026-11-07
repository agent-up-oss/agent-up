using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// Reads and arranges the Server's task queue. The Server owns the queue and decides every claim,
/// so this holds no state of its own: each change is sent and the list is read back.
/// </summary>
public sealed class TaskQueueService(ITaskQueueApiProvider api)
{
    public Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken) =>
        api.ListAsync(cancellationToken);

    public Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken) =>
        api.ListIssuesAsync(workspaceId, cancellationToken);

    public Task EnqueueAsync(string workspaceId, string issueId, string? targetWorkspaceId, CancellationToken cancellationToken) =>
        api.EnqueueAsync(workspaceId, issueId, targetWorkspaceId, cancellationToken);

    public Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken) =>
        api.RemoveAsync(task, cancellationToken);

    public Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken) =>
        api.MoveAsync(task, index, cancellationToken);

    public Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken) =>
        api.TargetAsync(task, workspaceId, cancellationToken);

    public Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken) =>
        api.ReleaseAsync(task, cancellationToken);
}
