using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.TaskSource.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.TaskSource.Controllers;

[ApiController]
[Route("api/task-source")]
public sealed class TaskSourceHttpController(TaskSourceController source) : ControllerBase
{
    [HttpGet]
    public ActionResult<TaskSourceDto> Get() => Ok(source.Describe());

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SaveTaskSourceTokenRequest request, CancellationToken cancellationToken)
    {
        var outcome = await source.SaveAsync(request, cancellationToken);
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpDelete]
    public ActionResult<TaskSourceDto> Clear() => Ok(source.Clear(null));

    [HttpDelete("{moduleId}")]
    public ActionResult<TaskSourceDto> Forget(string moduleId) => Ok(source.Clear(moduleId));

    [HttpGet("repositories")]
    public async Task<ActionResult<IReadOnlyList<TaskSourceRepositoryDto>>> Repositories(CancellationToken cancellationToken)
        => Ok(await source.ListRepositoriesAsync(cancellationToken));

    [HttpGet("issues")]
    public async Task<ActionResult<IReadOnlyList<ForgeIssueDto>>> Issues(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var issues = await source.ListIssuesAsync(moduleId, owner, repository, cancellationToken);
        return Ok(issues.Select(issue => new ForgeIssueDto(
            issue.Id,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.Url)));
    }

    [HttpGet("pulls")]
    public async Task<ActionResult<IReadOnlyList<TaskSourcePullDto>>> Pulls(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
        => Ok(await source.ListPullsAsync(moduleId, owner, repository, cancellationToken));
}
