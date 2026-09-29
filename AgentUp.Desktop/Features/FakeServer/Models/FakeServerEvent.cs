using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Features.FakeServer.Models;

public sealed record FakeServerEvent(long Sequence, string Type, JsonNode Payload, DateTimeOffset Timestamp)
{
    public string ToSseFrame()
    {
        var payload = new JsonObject
        {
            ["sequence"] = Sequence,
            ["type"] = Type,
            ["payload"] = Payload.DeepClone(),
            ["timestamp"] = Timestamp.ToString("O")
        };
        return $"data: {payload.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web))}\n\n";
    }
}
