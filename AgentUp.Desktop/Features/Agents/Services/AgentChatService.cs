using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;

namespace AgentUp.Desktop.Features.Agents.Services;

public sealed class AgentChatService(IAgentApiProvider provider)
{
    public Task<AgentSessionDto?> GetAsync(string id, CancellationToken cancellationToken) => provider.GetAsync(id, cancellationToken);
    public Task<AgentSessionDto?> ScheduleAsync(string id, string agent, CancellationToken cancellationToken) => provider.ScheduleAsync(id, agent, cancellationToken);
    public Task SendAsync(string id, string message, CancellationToken cancellationToken) => provider.SendAsync(id, message, cancellationToken);
    public Task AuthenticateAsync(string id, string methodId, CancellationToken cancellationToken) => provider.AuthenticateAsync(id, methodId, cancellationToken);
    public Task DecideAsync(string id, string request, string option, CancellationToken cancellationToken) => provider.DecideAsync(id, request, option, cancellationToken);
    public Task StopAsync(string id, CancellationToken cancellationToken) => provider.StopAsync(id, cancellationToken);
    public IAsyncEnumerable<AgentEventDto> EventsAsync(string id, long after, CancellationToken cancellationToken) => provider.EventsAsync(id, after, cancellationToken);
}
