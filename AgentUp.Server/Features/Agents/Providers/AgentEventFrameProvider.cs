using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentEventFrameProvider
{
    public JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);
    public string Frame(AgentEventDto item) => $"id: {item.Sequence}\nevent: {item.Type}\ndata: {JsonSerializer.Serialize(item)}\n\n";
}
