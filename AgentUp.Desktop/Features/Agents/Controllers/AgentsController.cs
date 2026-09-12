using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Services;

namespace AgentUp.Desktop.Features.Agents.Controllers;

public sealed class AgentsController(AgentChatService service)
{
    public Task<AgentSessionDto?> GetAsync(string id, CancellationToken cancellationToken) => service.GetAsync(id, cancellationToken);
    public Task<AgentSessionDto?> ScheduleAsync(string id, string agent, CancellationToken cancellationToken) => service.ScheduleAsync(id, agent, cancellationToken);
    public Task SendAsync(string id, string message, CancellationToken cancellationToken) => service.SendAsync(id, message, cancellationToken);
    public Task AuthenticateAsync(string id, string methodId, CancellationToken cancellationToken) => service.AuthenticateAsync(id, methodId, cancellationToken);
    public Task DecideAsync(string id, string request, string option, CancellationToken cancellationToken) => service.DecideAsync(id, request, option, cancellationToken);
    public Task StopAsync(string id, CancellationToken cancellationToken) => service.StopAsync(id, cancellationToken);
    public IAsyncEnumerable<AgentEventDto> EventsAsync(string id, long after, CancellationToken cancellationToken) => service.EventsAsync(id, after, cancellationToken);
}
