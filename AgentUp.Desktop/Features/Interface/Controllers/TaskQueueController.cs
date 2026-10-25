using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Features.Interface.Controllers;

/// <summary>The Tasks tab's way in to the Server's queue.</summary>
public sealed class TaskQueueController(TaskQueueService service)
{
    public Task<IReadOnlyList<QueuedTask>> ListAsync(CancellationToken cancellationToken) =>
        service.ListAsync(cancellationToken);

    public Task<IReadOnlyList<OfferedIssue>> ListIssuesAsync(string workspaceId, CancellationToken cancellationToken) =>
        service.ListIssuesAsync(workspaceId, cancellationToken);

    public Task EnqueueAsync(string workspaceId, string issueId, string? targetWorkspaceId, CancellationToken cancellationToken) =>
        service.EnqueueAsync(workspaceId, issueId, targetWorkspaceId, cancellationToken);

    public Task RemoveAsync(QueuedTask task, CancellationToken cancellationToken) =>
        service.RemoveAsync(task, cancellationToken);

    public Task MoveAsync(QueuedTask task, int index, CancellationToken cancellationToken) =>
        service.MoveAsync(task, index, cancellationToken);

    public Task TargetAsync(QueuedTask task, string? workspaceId, CancellationToken cancellationToken) =>
        service.TargetAsync(task, workspaceId, cancellationToken);

    public Task ReleaseAsync(QueuedTask task, CancellationToken cancellationToken) =>
        service.ReleaseAsync(task, cancellationToken);
}
