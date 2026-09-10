using AgentUp.Server.Features.Agents.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/agent")]
public sealed class AgentsHttpController(AgentsController agents) : ControllerBase
{
    [HttpGet]
    public ActionResult<AgentSessionDto> Get(string workspaceId) => FoundResult(this, agents.Get(workspaceId));

    [HttpPost]
    public async Task<IActionResult> Schedule(string workspaceId, ScheduleAgentRequest request)
    {
        var result = await agents.ScheduleAsync(workspaceId, request.Agent, HttpContext.RequestAborted);
        return ScheduleResult(this, result);
    }

    [HttpPost("messages")]
    public Task<IActionResult> Prompt(string workspaceId, AgentPromptRequest request) =>
        PromptRequestAsync(this, agents, workspaceId, request, HttpContext.RequestAborted);

    [HttpPost("authenticate")]
    public IActionResult Authenticate(string workspaceId, AgentAuthenticationRequest request) =>
        ActionResult(this, agents.Authenticate(workspaceId, request.MethodId), accepted: true);

    private static async Task<IActionResult> PromptRequestAsync(
        AgentsHttpController controller,
        AgentsController agents,
        string workspaceId,
        AgentPromptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            controller.ModelState.AddModelError("message", "Message is required.");
            return controller.ValidationProblem();
        }
        return ActionResult(controller, await agents.PromptAsync(workspaceId, request.Message, cancellationToken), accepted: true);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string workspaceId) => ActionResult(this, await agents.CancelAsync(workspaceId, HttpContext.RequestAborted));

    [HttpPost("permissions")]
    public IActionResult Decide(string workspaceId, AgentPermissionResponse response) => agents.Decide(workspaceId, response) ? NoContent() : NotFound();

    [HttpDelete]
    public async Task<IActionResult> Stop(string workspaceId) => ActionResult(this, await agents.StopAsync(workspaceId, HttpContext.RequestAborted));

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

    private static ActionResult<AgentSessionDto> FoundResult(ControllerBase controller, AgentSessionDto? session) =>
        session is null ? controller.NotFound() : controller.Ok(session);

    private static IActionResult ActionResult(ControllerBase controller, AgentActionResult result, bool accepted = false)
    {
        if (!result.Found) return controller.NotFound();
        if (result.Error is not null) return controller.Problem(statusCode: 409, title: "Agent operation failed", detail: result.Error);
        return accepted ? controller.Accepted() : controller.NoContent();
    }
}
