using System.Text.Json;
using System.Text.RegularExpressions;
using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Accepts the tagged JSON Qwen emits when it follows instructions, and the looser call
/// shapes a small model produces when it does not: bare JSON, <c>name(arg)</c>, and
/// <c>name(key=value)</c>. A copied template that still says TOOL_NAME is not a parse.
/// </summary>
public sealed class ToolCallParser : IToolCallParser
{
    private static readonly Regex TaggedJson = new(
        @"<tool_call>\s*(\{.*?\})\s*</tool_call>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex NamedJson = new(
        @"\{\s*""name""\s*:\s*""(?<name>[^""]+)""\s*,\s*""arguments""\s*:\s*(?<args>\{.*?\})",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CallSite = new(
        @"\b(?<name>complete_task|read_file|write_file|list_files)\s*\((?<args>[^)]*)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryParse(string text, out ParsedToolCall? call)
    {
        call = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var tagged = TaggedJson.Match(text);
        if (tagged.Success && TryReadJson(tagged.Groups[1].Value, out call))
            return true;

        var named = NamedJson.Match(text);
        if (named.Success && TryReadJsonObject(named.Groups["name"].Value, named.Groups["args"].Value, out call))
            return true;

        var site = CallSite.Match(text);
        if (site.Success)
        {
            call = new ParsedToolCall(
                site.Groups["name"].Value.ToLowerInvariant(),
                ReadCallArguments(site.Groups["name"].Value.ToLowerInvariant(), site.Groups["args"].Value));
            return true;
        }

        return false;
    }

    private static bool TryReadJson(string json, out ParsedToolCall? call)
    {
        call = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("name", out var nameProperty))
                return false;
            var name = nameProperty.GetString();
            if (string.IsNullOrWhiteSpace(name) || name == "TOOL_NAME")
                return false;
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
            if (document.RootElement.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in args.EnumerateObject())
                    arguments[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.GetRawText();
            }

            call = new ParsedToolCall(name, arguments);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadJsonObject(string name, string argumentsJson, out ParsedToolCall? call)
    {
        if (name == "TOOL_NAME")
        {
            call = null;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                arguments[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText();
            call = new ParsedToolCall(name, arguments);
            return true;
        }
        catch (JsonException)
        {
            call = null;
            return false;
        }
    }

    private static IReadOnlyDictionary<string, string> ReadCallArguments(string name, string raw)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
            return arguments;

        var assigned = Regex.Match(trimmed, @"^(?<key>[A-Za-z_][\w]*)\s*=\s*(?<value>.*)$");
        if (assigned.Success)
        {
            arguments[assigned.Groups["key"].Value] = Unquote(assigned.Groups["value"].Value);
            return arguments;
        }

        var positional = name switch
        {
            "complete_task" => "summary",
            "read_file" => "path",
            "write_file" => "path",
            "list_files" => "path",
            _ => "value"
        };
        arguments[positional] = Unquote(trimmed);
        return arguments;
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
            return trimmed[1..^1];
        return trimmed;
    }
}
