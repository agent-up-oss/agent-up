using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Ollama's <c>/api/chat</c> with tool calling, non-streaming. Any server that speaks that shape
/// works; the endpoint and model come from <c>AGENTUP_OLLAMA_URL</c> and <c>AGENTUP_OLLAMA_MODEL</c>.
/// </summary>
public sealed class OllamaChatModelProvider(IHttpClientFactory clients, IConfiguration configuration) : IInProcessChatModel
{
    public const string HttpClientName = "in-process-agent-chat";

    public bool Configured => Settings() is not null;

    public async Task<InProcessChatMessage> ChatAsync(
        IReadOnlyList<InProcessChatMessage> messages,
        IReadOnlyList<InProcessTool> tools,
        CancellationToken cancellationToken)
    {
        var settings = Settings()
            ?? throw new InvalidOperationException($"{InProcessAgents.OllamaModelSetting} is not set.");
        var client = clients.CreateClient(HttpClientName);
        try
        {
            using var response = await client.PostAsJsonAsync(
                new Uri(settings.Endpoint, "api/chat"),
                Request(settings.Model, messages, tools),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"The chat model returned {(int)response.StatusCode}: {Truncate(detail)}");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            return Parse(document.RootElement);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException($"The chat model at {settings.Endpoint} is unreachable: {exception.Message}", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The chat model returned a response that is not JSON.", exception);
        }
    }

    public InProcessChatSettings? Settings()
    {
        var model = configuration[InProcessAgents.OllamaModelSetting];
        if (string.IsNullOrWhiteSpace(model))
            return null;
        var url = configuration[InProcessAgents.OllamaUrlSetting];
        if (string.IsNullOrWhiteSpace(url))
            url = InProcessAgents.OllamaDefaultUrl;
        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https"))
            return null;
        return new InProcessChatSettings(endpoint, model.Trim());
    }

    public static JsonObject Request(
        string model,
        IReadOnlyList<InProcessChatMessage> messages,
        IReadOnlyList<InProcessTool> tools)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["stream"] = false,
            ["messages"] = new JsonArray([.. messages.Select(Message)])
        };
        if (tools.Count > 0)
            body["tools"] = new JsonArray([.. tools.Select(Tool)]);
        return body;
    }

    public static InProcessChatMessage Parse(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The chat model response has no message.");
        var content = message.TryGetProperty("content", out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString() ?? ""
            : "";
        var calls = message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array
            ? toolCalls.EnumerateArray().Select(ToolCall).OfType<InProcessToolCall>().ToList()
            : [];

        return new InProcessChatMessage("assistant", content, calls.Count == 0 ? null : calls);
    }

    private static InProcessToolCall? ToolCall(JsonElement call) =>
        call.TryGetProperty("function", out var function)
        && function.TryGetProperty("name", out var name)
        && name.GetString() is { Length: > 0 } toolName
        && !string.IsNullOrWhiteSpace(toolName)
            ? new InProcessToolCall(toolName, Arguments(function))
            : null;

    private static JsonElement Arguments(JsonElement function)
    {
        if (!function.TryGetProperty("arguments", out var arguments))
            return EmptyObject();
        // OpenAI-shaped servers send arguments as a JSON string; Ollama sends an object.
        if (arguments.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var parsed = JsonDocument.Parse(arguments.GetString() ?? "{}");
                return parsed.RootElement.ValueKind == JsonValueKind.Object ? parsed.RootElement.Clone() : EmptyObject();
            }
            catch (JsonException)
            {
                return EmptyObject();
            }
        }

        return arguments.ValueKind == JsonValueKind.Object ? arguments.Clone() : EmptyObject();
    }

    private static JsonObject Message(InProcessChatMessage message)
    {
        var node = new JsonObject { ["role"] = message.Role, ["content"] = message.Content };
        if (message.ToolName is not null)
            node["tool_name"] = message.ToolName;
        if (message.Images is { Count: > 0 } images)
            node["images"] = new JsonArray([.. images.Select(image => (JsonNode?)JsonValue.Create(image))]);
        if (message.ToolCalls is { Count: > 0 } calls)
        {
            node["tool_calls"] = new JsonArray([.. calls.Select(call => (JsonNode?)new JsonObject
            {
                ["function"] = new JsonObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = JsonNode.Parse(call.Arguments.GetRawText())
                }
            })]);
        }

        return node;
    }

    private static JsonObject Tool(InProcessTool tool) => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["parameters"] = JsonNode.Parse(tool.Parameters.GetRawText())
        }
    };

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new { });

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];
}
