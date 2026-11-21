using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Splits what Qwen generated into the text and the <c>&lt;tool_call&gt;</c> blocks an OpenAI
/// client expects as separate fields. A call to a tool the request did not offer stays text, so a
/// small model naming something it imagined is answered rather than executed.
/// </summary>
public sealed class ChatCompletionReader
{
    private static readonly Regex Tagged = new(
        @"<tool_call>\s*(?<json>\{.*?\})\s*</tool_call>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public ChatCompletion Read(string generated, IReadOnlyCollection<string> offeredTools, int promptTokens, int completionTokens)
    {
        var calls = new List<ChatToolCall>();
        var content = Tagged.Replace(generated, match =>
        {
            var call = TryRead(match.Groups["json"].Value, offeredTools, calls.Count);
            if (call is null)
                return match.Value;
            calls.Add(call);
            return "";
        });
        content = content.Replace("<|im_end|>", "", StringComparison.Ordinal).Trim();
        return new ChatCompletion(content, calls, promptTokens, completionTokens);
    }

    private static ChatToolCall? TryRead(string json, IReadOnlyCollection<string> offeredTools, int index)
    {
        try
        {
            // The tag pattern only captures an object, so a parse that succeeds is never null.
            var node = JsonNode.Parse(json)!;
            var name = node["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name) || !offeredTools.Contains(name))
                return null;
            var arguments = node["arguments"] switch
            {
                JsonObject value => value.ToJsonString(),
                JsonValue value when value.TryGetValue<string>(out var text) => text,
                _ => "{}"
            };
            return new ChatToolCall($"call_{index}_{Guid.NewGuid():N}"[..20], name, arguments);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
