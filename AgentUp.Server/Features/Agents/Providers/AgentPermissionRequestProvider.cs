using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Completes a permission request with what the agent already announced about the same tool call.
/// OpenCode asks before it has named the command in the request itself (title <c>bash</c>,
/// <c>rawInput</c> holding only <c>cwd</c>) while the update it sent just before carries the
/// command, and a policy scoped to a command can only judge the command.
/// </summary>
public static class AgentPermissionRequestProvider
{
    private static readonly string[] Announced = ["rawInput", "title", "kind", "locations"];

    public static JsonElement WithAnnouncedToolCall(JsonElement request, ConcurrentDictionary<string, JsonElement> toolCalls)
    {
        if (request.ValueKind != JsonValueKind.Object
            || !request.TryGetProperty("toolCall", out var toolCall)
            || toolCall.ValueKind != JsonValueKind.Object
            || !toolCall.TryGetProperty("toolCallId", out var id)
            || id.ValueKind != JsonValueKind.String
            || !toolCalls.TryGetValue(id.GetString()!, out var announced))
            return request;

        var merged = JsonNode.Parse(request.GetRawText())!.AsObject();
        var call = merged["toolCall"]!.AsObject();
        var fills = Announced
            .Where(name => announced.TryGetProperty(name, out _))
            .Select(name => (Name: name, Value: JsonNode.Parse(announced.GetProperty(name).GetRawText())))
            .Where(fill => call[fill.Name] is null || (fill.Name == "rawInput" && !HasCommand(call[fill.Name]) && HasCommand(fill.Value)))
            .ToArray();
        foreach (var (name, value) in fills)
            call[name] = value;

        return JsonSerializer.SerializeToElement(merged);
    }

    private static bool HasCommand(JsonNode? rawInput) =>
        rawInput is JsonObject input && input["command"] is JsonValue command && command.TryGetValue<string>(out var text) && text.Length > 0;
}
