using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Features.Scripted.Providers;

/// <summary>
/// Reads the MCP calls a prompt directs. A directive is a prompt whose whole text is
/// <c>{"mcp":[{"server":"agent-up-browser","tool":"browser_navigate","arguments":{...}}]}</c>.
/// The script travels in the prompt rather than the environment because the Server starts every
/// agent in a sandbox that does not pass its own environment through.
/// </summary>
public static class ScriptedMcpDirectiveProvider
{
    public static IReadOnlyList<ScriptedMcpCall>? Parse(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || !prompt.TrimStart().StartsWith('{'))
            return null;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(prompt);
        }
        catch (JsonException)
        {
            return null;
        }
        if (root?["mcp"] is not JsonArray calls)
            return null;
        return calls
            .OfType<JsonObject>()
            .Where(call => call["server"] is JsonValue && call["tool"] is JsonValue)
            .Select(call => new ScriptedMcpCall(
                call["server"]!.GetValue<string>(),
                call["tool"]!.GetValue<string>(),
                call["arguments"] as JsonObject is { } arguments ? (JsonObject)arguments.DeepClone() : new JsonObject()))
            .ToArray();
    }

    /// <summary>The text of an ACP <c>session/prompt</c>: its text content blocks, joined.</summary>
    public static string PromptText(JsonNode? parameters) =>
        string.Join("\n", (parameters?["prompt"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(block => block["type"]?.GetValue<string>() == "text")
            .Select(block => block["text"]?.GetValue<string>() ?? string.Empty));

    /// <summary>The HTTP MCP servers in <c>session/new</c> or <c>session/load</c> parameters.</summary>
    public static IReadOnlyList<ScriptedMcpServer> Servers(JsonNode? parameters) =>
        (parameters?["mcpServers"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(server => server["name"] is JsonValue && server["url"] is JsonValue)
            .Select(server => new ScriptedMcpServer(
                server["name"]!.GetValue<string>(),
                server["url"]!.GetValue<string>(),
                (server["headers"] as JsonArray ?? [])
                    .OfType<JsonObject>()
                    .Where(header => header["name"] is JsonValue && header["value"] is JsonValue)
                    .ToDictionary(header => header["name"]!.GetValue<string>(), header => header["value"]!.GetValue<string>())))
            .ToArray();
}
