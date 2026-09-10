using AgentUp.Desktop.Features.Agents.DTOs;

namespace AgentUp.Desktop.Features.Agents.Interfaces;

public interface IAgentApiProvider
{
    Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken);
    Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken);
    Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken);
    Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken);
    Task StopAsync(string workspaceId, CancellationToken cancellationToken);
    IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, CancellationToken cancellationToken);
}
