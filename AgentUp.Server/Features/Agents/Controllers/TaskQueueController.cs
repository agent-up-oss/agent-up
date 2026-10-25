using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>
/// The boundary around the picked queue: what is queued, what gets picked, and who may take it.
/// </summary>
public sealed class TaskQueueController(TaskQueueService queue, TaskPickingService picking)
{
    public IReadOnlyList<QueuedTaskDto> List() => [.. queue.List().Select(Dto)];

    public async Task<QueuedTaskDto?> EnqueueAsync(
        EnqueueTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await picking.PickAsync(
            request.WorkspaceId,
            request.IssueId,
            request.TargetWorkspaceId,
            cancellationToken);
        return task is null ? null : Dto(task);
    }

    public bool Remove(string owner, string repository, string id) => queue.Remove(owner, repository, id);

    public bool Move(string owner, string repository, string id, int index) =>
        queue.Move(owner, repository, id, index);

    public bool Target(string owner, string repository, string id, string? workspaceId) =>
        queue.Target(owner, repository, id, workspaceId);

    public bool Release(string owner, string repository, string id) => queue.Release(owner, repository, id);

    private static QueuedTaskDto Dto(QueuedTask task) => new(
        task.Id,
        task.Owner,
        task.Repository,
        task.Number,
        task.Title,
        task.Body,
        task.Url,
        task.QueuedAt,
        task.TargetWorkspaceId,
        task.HeldByWorkspaceId);
}
