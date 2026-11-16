using AgentUp.Server.Features.Agents.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TaskQueueHttpController(TaskQueueController tasks) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<QueuedTaskDto>> Get() => Ok(tasks.List());

    [HttpPost]
    public async Task<IActionResult> Enqueue(
        [FromBody] EnqueueTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await tasks.EnqueueAsync(request, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpDelete("{owner}/{repository}/{id}")]
    public IActionResult Remove(string owner, string repository, string id) =>
        tasks.Remove(owner, repository, id)
            ? NoContent()
            : Problem(
                statusCode: 409,
                title: "The task was not removed",
                detail: "No free task with that id is queued. A task a workspace is carrying stays until it finishes or is released.");

    [HttpPut("{owner}/{repository}/{id}/position")]
    public IActionResult Move(string owner, string repository, string id, [FromBody] MoveTaskRequest request) =>
        tasks.Move(owner, repository, id, request.Index) ? NoContent() : NotFound();

    [HttpPut("{owner}/{repository}/{id}/target")]
    public IActionResult Target(string owner, string repository, string id, [FromBody] TargetTaskRequest request) =>
        tasks.Target(owner, repository, id, request.WorkspaceId) ? NoContent() : NotFound();

    [HttpPost("{owner}/{repository}/{id}/release")]
    public IActionResult Release(string owner, string repository, string id) =>
        tasks.Release(owner, repository, id) ? NoContent() : NotFound();
}
