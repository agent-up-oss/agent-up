using AgentUp.Server.Features.Agents.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/agent")]
public sealed class AgentsHttpController(AgentsController agents) : ControllerBase
{
    [HttpGet]
    public ActionResult<AgentSessionDto> Get(string workspaceId) => Ok(agents.Get(workspaceId));

    [HttpPost]
    public async Task<IActionResult> Schedule(string workspaceId, ScheduleAgentRequest request)
    {
        var result = await agents.ScheduleAsync(workspaceId, request.Agent, HttpContext.RequestAborted);
        return ScheduleResult(this, result);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> Prompt(string workspaceId, AgentPromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["Message is required."] });
        return await agents.PromptAsync(workspaceId, request.Message, HttpContext.RequestAborted) ? Accepted() : NotFound();
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string workspaceId) => await agents.CancelAsync(workspaceId, HttpContext.RequestAborted) ? NoContent() : NotFound();

    [HttpPost("permissions")]
    public IActionResult Decide(string workspaceId, AgentPermissionResponse response) => agents.Decide(workspaceId, response) ? NoContent() : NotFound();

    [HttpDelete]
    public async Task<IActionResult> Stop(string workspaceId) => await agents.StopAsync(workspaceId, HttpContext.RequestAborted) ? NoContent() : NotFound();

    [HttpGet("events")]
    public Task Events(string workspaceId, [FromQuery] long after = 0) =>
        agents.WriteEventsAsync(workspaceId, after, Response, HttpContext.RequestAborted);

    private static IActionResult ScheduleResult(ControllerBase controller, AgentScheduleResult result)
    {
        if (!result.Found) return controller.NotFound();
        return result.Error is null
            ? controller.Ok(result.Session)
            : controller.Problem(statusCode: 409, title: "Agent could not be scheduled", detail: result.Error);
    }
}
