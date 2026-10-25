using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Features.Agents.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/agent")]
public sealed class AgentsHttpController(
    AgentsController agents,
    AutonomousWorkspaceService? autonomy = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AgentSessionDto>> Get(string workspaceId, CancellationToken cancellationToken) =>
        FoundResult(this, await agents.GetAsync(workspaceId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Schedule(string workspaceId, ScheduleAgentRequest request)
    {
        var result = await agents.ScheduleAsync(workspaceId, request.Agent, HttpContext.RequestAborted);
        return ScheduleResult(this, result);
    }

    [HttpPut("binding")]
    public async Task<IActionResult> Bind(string workspaceId, BindAgentRequest request)
    {
        var result = await agents.BindAsync(workspaceId, request.Agent, HttpContext.RequestAborted);
        return BindingResult(this, result);
    }

    [HttpDelete("binding")]
    public async Task<IActionResult> Unbind(string workspaceId)
    {
        var result = await agents.UnbindAsync(workspaceId, HttpContext.RequestAborted);
        return BindingResult(this, result);
    }

    [HttpGet("policy")]
    public IActionResult GetPolicy(string workspaceId) =>
        agents.Get(workspaceId) is null ? NotFound() : Ok(PolicyPayload(workspaceId));

    [HttpPut("policy")]
    public async Task<IActionResult> PutPolicy(string workspaceId, PermissionPolicyRequest request)
    {
        var rules = (request.Rules ?? []).Select(ToRule).ToArray();
        return await AutonomyActionAsync(workspaceId, current => current.SetPolicyAsync(workspaceId, rules));
    }

    [HttpGet("role")]
    public IActionResult GetRole(string workspaceId) =>
        agents.Get(workspaceId) is null ? NotFound() : RoleResult(workspaceId);

    [HttpPut("role")]
    public async Task<IActionResult> PutRole(string workspaceId, AgentRoleRequest request) =>
        await AutonomyActionAsync(workspaceId, current => current.SetRoleAsync(workspaceId, ToRole(request)));

    [HttpDelete("role")]
    public async Task<IActionResult> DeleteRole(string workspaceId) =>
        await AutonomyActionAsync(workspaceId, current => current.SetRoleAsync(workspaceId, null));

    [HttpPut("host-connection")]
    public async Task<IActionResult> PutHostConnection(string workspaceId, HostConnectionRequest request) =>
        await AutonomyActionAsync(workspaceId, current => current.SetHostConnectionAsync(workspaceId, request.Authorized));

    [HttpPut("queue-order")]
    public async Task<IActionResult> PutQueueOrder(string workspaceId, QueueOrderRequest request) =>
        await AutonomyActionAsync(workspaceId, current => current.SetQueueOrderAsync(workspaceId, request.Order));

    [HttpPut("autonomy")]
    public Task<IActionResult> PutAutonomy(string workspaceId, AutonomyActivationRequest request) =>
        AutonomyActionAsync(workspaceId, current => request.Active
            ? current.ActivateAsync(workspaceId, HttpContext.RequestAborted)
            : Task.FromResult(current.Deactivate(workspaceId)));

    [HttpGet("autonomy")]
    public Task<IActionResult> GetAutonomy(string workspaceId) =>
        AutonomyStatusAsync(this, agents, autonomy, workspaceId, HttpContext.RequestAborted);

    [HttpGet("loop")]
    public IActionResult GetLoop(string workspaceId) =>
        agents.Get(workspaceId) is null ? NotFound() : Ok(LoopPayload(workspaceId));

    [HttpPost("sessions/{sessionId}/resume")]
    public async Task<IActionResult> Resume(string workspaceId, string sessionId)
    {
        var result = await agents.ResumeAsync(workspaceId, sessionId, HttpContext.RequestAborted);
        return ScheduleResult(this, result);
    }

    [HttpPost("messages")]
    public Task<IActionResult> Prompt(string workspaceId, AgentPromptRequest request) =>
        PromptRequestAsync(this, agents, workspaceId, request, HttpContext.RequestAborted);

    [HttpPost("authenticate")]
    public IActionResult Authenticate(string workspaceId, AgentAuthenticationRequest request) =>
        ActionResult(this, agents.Authenticate(workspaceId, request.MethodId), accepted: true);

    /// <summary>A code the user copied out of the provider page, for a CLI waiting on stdin.</summary>
    [HttpPost("login/code")]
    public IActionResult SubmitLoginCode(string workspaceId, AgentLoginCodeRequest request) =>
        ActionResult(this, agents.SubmitLoginCode(workspaceId, request.Code), accepted: true);

    /// <summary>
    /// A redirect the client intercepted on the agent CLI's behalf. The CLI listens on loopback
    /// on this host, so when the browser is on another device the redirect only gets here if the
    /// client hands it back.
    /// </summary>
    [HttpPost("login/callback")]
    public IActionResult SubmitLoginCallback(string workspaceId, AgentLoginCallbackRequest request) =>
        ActionResult(this, agents.SubmitLoginCallback(workspaceId, request.Url), accepted: true);

    private static async Task<IActionResult> AutonomyStatusAsync(
        AgentsHttpController controller,
        AgentsController agents,
        AutonomousWorkspaceService? autonomy,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        if (autonomy is null)
            return controller.Problem(statusCode: 409, title: "Agent operation failed", detail: "Autonomy is not configured.");
        if (agents.Get(workspaceId) is null)
            return controller.NotFound();
        return controller.Ok(await autonomy.GetStatusAsync(workspaceId, cancellationToken));
    }

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

    private IActionResult RoleResult(string workspaceId)
    {
        var role = autonomy?.Role(workspaceId);
        return role is null ? NoContent() : Ok(ToDto(role));
    }

    private PermissionPolicyRequest PolicyPayload(string workspaceId) =>
        new() { Rules = (autonomy?.PolicyRules(workspaceId) ?? []).Select(ToDto).ToArray() };

    private AgentLoopStatusDto? LoopPayload(string workspaceId) =>
        agents.Get(workspaceId)?.Loop ?? ToDto(autonomy?.Status(workspaceId));

    private async Task<IActionResult> AutonomyActionAsync(string workspaceId, Func<AutonomousWorkspaceService, Task<AgentActionResult>> action)
    {
        if (autonomy is null)
            return Problem(statusCode: 409, title: "Agent operation failed", detail: "Autonomy is not configured.");
        return ActionResult(this, await action(autonomy));
    }

    private static PermissionRule ToRule(PermissionRuleDto dto) => new(dto.Id, dto.Kind, dto.Allow, dto.Scope);

    private static PermissionRuleDto ToDto(PermissionRule rule) => new(rule.Id, rule.Kind, rule.Allow, rule.Scope);

    private static AgentRoleDefinition ToRole(AgentRoleRequest request) =>
        new(request.Prompt, request.Tools, request.Trigger, request.Termination);

    private static AgentRoleRequest ToDto(AgentRoleDefinition role) => new()
    {
        Prompt = role.Prompt,
        Tools = role.Tools,
        Trigger = role.Trigger,
        Termination = role.Termination
    };

    private static AgentLoopStatusDto? ToDto(LoopStatusSnapshot? loop) =>
        loop is null
            ? null
            : new AgentLoopStatusDto(
                loop.Active,
                loop.Phase,
                loop.CurrentTask?.Id,
                loop.CurrentTask?.Number,
                loop.CurrentTask?.Title,
                loop.Terminal?.ToString(),
                loop.StopReason,
                loop.BlockReason,
                loop.PermissionRuleIds);

    private static IActionResult BindingResult(ControllerBase controller, AgentBindingResult result)
    {
        if (!result.Found) return controller.NotFound();
        return result.Error is null
            ? controller.Ok(result.Session)
            : controller.Problem(statusCode: result.Status, title: "Agent could not be bound", detail: result.Error);
    }

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
