using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Renders an OpenAI chat-completions request into Qwen2.5's ChatML, tools included in Qwen's own
/// <c>&lt;tools&gt;</c> format so the model answers with <c>&lt;tool_call&gt;</c> blocks.
/// </summary>
/// <remarks>
/// A coding agent's system prompt and tool schemas run to tens of thousands of tokens, which a
/// 1.5B model on a CPU cannot read in a test's time budget and could not follow anyway. The prompt
/// is compacted: the system prompt is cut, each tool keeps its name, first sentence and parameter
/// names, and only the latest messages are kept. That is what makes a real agent loop over the
/// pinned in-process model finish in seconds rather than minutes.
/// </remarks>
public sealed class ChatPromptProvider
{
    public const int SystemPromptChars = 1200;
    public const int MessageChars = 1500;
    public const int MaxMessages = 8;

    /// <summary>
    /// Renders the request. <paramref name="narrowTools"/> drops MCP-contributed tools the
    /// conversation has not named; an Ollama client sends only its own tools, so it renders them all.
    /// </summary>
    public string Render(JsonNode request, bool narrowTools = true)
    {
        var builder = new StringBuilder();
        var messages = request["messages"] as JsonArray ?? [];
        var system = string.Join("\n\n", messages
            .Where(message => Role(message) == "system")
            .Select(message => Text(message?["content"])));
        builder.Append("<|im_start|>system\n")
            .Append(Cut(system.Length == 0 ? "You are a helpful coding assistant." : system, SystemPromptChars));
        var offered = request["tools"] as JsonArray ?? [];
        var tools = narrowTools ? Offered(offered, messages) : [.. offered];
        if (tools.Count > 0)
        {
            builder.Append("\n\n# Tools\n\nYou may call one or more functions to assist with the user query.\n\n")
                .Append("You are provided with function signatures within <tools></tools> XML tags:\n<tools>\n");
            foreach (var tool in tools)
                builder.Append(CompactTool(tool)).Append('\n');
            builder.Append("</tools>\n\nFor each function call, return a json object with function name and arguments within <tool_call></tool_call> XML tags:\n")
                .Append("<tool_call>\n{\"name\": <function-name>, \"arguments\": <args-json-object>}\n</tool_call>");
        }

        builder.Append("<|im_end|>\n");
        foreach (var message in messages.Where(message => Role(message) != "system").TakeLast(MaxMessages))
            builder.Append(RenderMessage(message));
        return builder.Append("<|im_start|>assistant\n").ToString();
    }

    /// <summary>
    /// The tools a 1.5B model can still choose between. The coding agent's own tools stay; a tool
    /// an MCP server contributed (named <c>server_tool</c>) stays only once the conversation names
    /// it, because dozens of them crowd out the agent's tools and the model stops calling any.
    /// </summary>
    internal static IReadOnlyList<JsonNode?> Offered(JsonArray tools, JsonArray messages)
    {
        var conversation = string.Join("\n", messages
            .Where(message => Role(message) != "system")
            .SelectMany(message => (message?["tool_calls"] as JsonArray ?? [])
                .Select(call => call?["function"]?["name"]?.GetValue<string>() ?? "")
                .Prepend(Text(message?["content"]))));
        return tools.Where(tool =>
        {
            var name = tool?["function"]?["name"]?.GetValue<string>() ?? "";
            var separator = name.IndexOf('_');
            return separator < 0
                   || conversation.Contains(name, StringComparison.Ordinal)
                   || conversation.Contains(name[(separator + 1)..], StringComparison.Ordinal);
        }).ToArray();
    }

    private static string RenderMessage(JsonNode? message)
    {
        var role = Role(message);
        var content = Cut(Text(message?["content"]), MessageChars);
        if (role == "tool")
            return $"<|im_start|>user\n<tool_response>\n{content}\n</tool_response><|im_end|>\n";

        var builder = new StringBuilder($"<|im_start|>{(role == "assistant" ? "assistant" : "user")}\n{content}");
        foreach (var call in message?["tool_calls"] as JsonArray ?? [])
        {
            var function = call?["function"];
            builder.Append("\n<tool_call>\n{\"name\": ")
                .Append(JsonSerializer.Serialize(function?["name"]?.GetValue<string>() ?? ""))
                .Append(", \"arguments\": ")
                .Append(Arguments(function?["arguments"]))
                .Append("}\n</tool_call>");
        }

        return builder.Append("<|im_end|>\n").ToString();
    }

    /// <summary>OpenAI sends a call's arguments as JSON text; Ollama sends the object itself.</summary>
    private static string Arguments(JsonNode? arguments) => arguments switch
    {
        JsonObject value => value.ToJsonString(),
        JsonValue value when value.TryGetValue<string>(out var text) && text.Length > 0 => text,
        _ => "{}"
    };

    private static string CompactTool(JsonNode? tool)
    {
        var function = tool?["function"];
        var description = FirstSentence(function?["description"]?.GetValue<string>() ?? "");
        var properties = new JsonObject();
        foreach (var (name, schema) in function?["parameters"]?["properties"] as JsonObject ?? [])
            properties[name] = new JsonObject { ["type"] = schema?["type"]?.DeepClone() ?? "string" };
        var compact = new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = function?["name"]?.GetValue<string>() ?? "",
                ["description"] = description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = function?["parameters"]?["required"]?.DeepClone() ?? new JsonArray()
                }
            }
        };
        return compact.ToJsonString();
    }

    private static string Role(JsonNode? message) => message?["role"]?.GetValue<string>() ?? "user";

    /// <summary>OpenAI content is a string or a list of parts; only the text parts reach the model.</summary>
    internal static string Text(JsonNode? content) => content switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonArray parts => string.Join("\n", parts
            .Where(part => part?["type"]?.GetValue<string>() == "text")
            .Select(part => part?["text"]?.GetValue<string>() ?? "")),
        _ => ""
    };

    private static string FirstSentence(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        var stop = line.IndexOf(". ", StringComparison.Ordinal);
        return Cut(stop > 0 ? line[..(stop + 1)] : line, 160);
    }

    private static string Cut(string text, int limit) => text.Length <= limit ? text : text[..limit] + " …";
}
