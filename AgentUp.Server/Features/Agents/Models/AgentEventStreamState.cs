using System.Threading.Channels;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

public sealed class AgentEventStreamState
{
    public object SyncRoot { get; } = new();
    public List<AgentEventDto> History { get; } = [];
    public Dictionary<Guid, Channel<AgentEventDto>> Subscribers { get; } = [];
}
