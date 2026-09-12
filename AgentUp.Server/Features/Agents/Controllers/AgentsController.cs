using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

public sealed class AgentsController(AgentSchedulingService scheduling, AgentEventService events)
{
    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) => scheduling.GetAsync(workspaceId, cancellationToken);
    public AgentSessionDto? Get(string workspaceId) => scheduling.Get(workspaceId);
    public Task<AgentScheduleResult> ScheduleAsync(string workspaceId, AgentKind kind, CancellationToken cancellationToken) => scheduling.ScheduleAsync(workspaceId, kind, cancellationToken);
    public Task<AgentActionResult> PromptAsync(string workspaceId, string message, CancellationToken cancellationToken) => scheduling.PromptAsync(workspaceId, message, cancellationToken);
    public AgentActionResult Authenticate(string workspaceId, string methodId) => scheduling.Authenticate(workspaceId, methodId);
    public Task<AgentActionResult> CancelAsync(string workspaceId, CancellationToken cancellationToken) => scheduling.CancelAsync(workspaceId, cancellationToken);
    public Task<AgentActionResult> StopAsync(string workspaceId, CancellationToken cancellationToken) => scheduling.StopAsync(workspaceId, cancellationToken);
    public bool Decide(string workspaceId, AgentPermissionResponse response) => scheduling.Decide(workspaceId, response);
    public IAsyncEnumerable<AgentEventDto> Events(string workspaceId, long after, CancellationToken cancellationToken) => events.SubscribeAsync(workspaceId, after, cancellationToken);
    public Task WriteEventsAsync(string workspaceId, long after, Microsoft.AspNetCore.Http.HttpResponse response, CancellationToken cancellationToken) => events.WriteAsync(workspaceId, after, response, cancellationToken);
}
