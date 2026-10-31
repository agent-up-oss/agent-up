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
    public ActionResult<TaskSourceDto> Set([FromBody] SetTaskSourceRequest request) => Ok(source.Set(request));

    [HttpDelete]
    public ActionResult<TaskSourceDto> Clear() => Ok(source.Clear());

    [HttpPost("login")]
    public IActionResult Login()
    {
        var outcome = source.StartLogin();
        return StatusCode(outcome.StatusCode, outcome.Body);
    }

    [HttpGet("issues")]
    public async Task<ActionResult<IReadOnlyList<ForgeIssueDto>>> Issues(CancellationToken cancellationToken)
    {
        var issues = await source.ListIssuesAsync(cancellationToken);
        return Ok(issues.Select(issue => new ForgeIssueDto(
            issue.Id,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.Url)));
    }
}
