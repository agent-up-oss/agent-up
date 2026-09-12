using System.Globalization;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.Providers;

public sealed record PresentedAgentUpdate(
    string Kind,
    string? Role = null,
    string? Text = null,
    string? ToolCallId = null,
    string? Status = null,
    string? Title = null,
    IReadOnlyList<string>? Locations = null,
    string? Mode = null,
    string? Usage = null,
    bool? Compacting = null,
    IReadOnlyList<AgentPlanEntry>? Entries = null);

public sealed record AgentPlanEntry(string Content, string Status);
public sealed record AgentPermissionPrompt(string RequestId, string Title, string? Detail, IReadOnlyList<string> Locations, IReadOnlyList<AgentPermissionChoice> Options);
public sealed record AgentPermissionChoice(string OptionId, string Name, string? Kind);

public static class AgentEventPresentationProvider
{
    public static JsonElement Unwrap(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("update", out var update) ? update : payload;

    public static PresentedAgentUpdate Present(JsonElement update)
    {
        var kind = StringField(update, "sessionUpdate") ?? StringField(update, "type") ?? "";
        if (kind.Length == 0 || kind.Contains("user_message", StringComparison.Ordinal) || kind.Contains("available_commands", StringComparison.Ordinal) || kind.Contains("config_option", StringComparison.Ordinal))
            return new("ignore");
        if (kind.Contains("thought", StringComparison.Ordinal))
        {
            var text = ContentText(update);
            return string.IsNullOrEmpty(text) ? new("ignore") : new("message", Role: "Thought", Text: text);
        }
        if (kind.Contains("tool", StringComparison.Ordinal)) return PresentTool(update);
        if (kind.Contains("plan", StringComparison.Ordinal))
        {
            var entries = PlanEntries(update);
            return entries.Count == 0 ? new("ignore") : new("plan", Role: "Plan", Entries: entries, Text: string.Join("\n", entries.Select(entry => $"{StatusMark(entry.Status)} {entry.Content}")));
        }
        if (kind.Contains("message", StringComparison.Ordinal))
        {
            var text = ContentText(update);
            return string.IsNullOrEmpty(text) ? new("ignore") : new("message", Role: "Agent", Text: text);
        }
        if (kind.Contains("session_info", StringComparison.Ordinal))
        {
            var title = StringField(update, "title");
            return string.IsNullOrEmpty(title) ? new("ignore") : new("context", Title: title);
        }
        if (kind.Contains("mode", StringComparison.Ordinal))
        {
            var mode = StringField(update, "currentModeId") ?? StringField(update, "mode");
            return string.IsNullOrEmpty(mode) ? new("ignore") : new("context", Mode: mode);
        }
        if (kind.Contains("usage", StringComparison.Ordinal))
        {
            var usage = FormatUsage(update);
            return usage is null ? new("ignore") : new("context", Usage: usage);
        }
        if (kind.Contains("compaction", StringComparison.Ordinal))
            return new("context", Compacting: !kind.Contains("summary", StringComparison.Ordinal));
        return new("ignore");
    }

    public static AgentPermissionPrompt? ParsePermission(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return null;
        var requestId = StringField(payload, "requestId");
        if (string.IsNullOrEmpty(requestId)) return null;
        var request = payload.TryGetProperty("request", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : payload;
        var options = PermissionOptions(request);
        if (options.Count == 0) return null;
        var toolCall = ObjectProperty(request, "toolCall") ?? ObjectProperty(ObjectProperty(request, "subject"), "toolCall");
        var subject = ObjectProperty(request, "subject");
        var command = StringField(subject, "type") == "command" ? subject : null;
        var locations = LocationsOf(toolCall).Concat(StringField(command, "cwd") is { } cwd ? [cwd] : Array.Empty<string>()).ToArray();
        var title = StringField(request, "title") ?? StringField(toolCall, "title") ?? StringField(command, "command") ?? "Agent requests permission";
        var detail = StringField(request, "description") ?? StringField(command, "command");
        if (detail == title) detail = null;
        return new(requestId, title, detail, locations, options);
    }

    public static string ActivityLabel(string? state, bool hasPermission, string? error, string? hintKind, string? toolTitle)
    {
        if (!string.IsNullOrWhiteSpace(error)) return error;
        if (state == "authentication_required") return "Waiting for sign-in";
        if (hasPermission) return "Waiting for a decision";
        if (state == "stopped") return "Stopped";
        if (state == "running")
        {
            return hintKind switch
            {
                "Thought" => "Thinking",
                "Tool" => string.IsNullOrWhiteSpace(toolTitle) ? "Using a tool" : $"Using {toolTitle}",
                "Agent" => "Writing",
                "Plan" => "Working through the plan",
                "Compacting" => "Compacting context",
                _ => "Working"
            };
        }
        return state is "ready" or "idle" or null ? "Idle" : state;
    }

    public static string OptionLabel(string name, string? kind, string optionId)
    {
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return kind switch
        {
            "allow_once" => "Allow once",
            "allow_always" => "Always allow",
            "reject_once" => "Reject",
            "reject_always" => "Always reject",
            _ => optionId
        };
    }

    private static PresentedAgentUpdate PresentTool(JsonElement update)
    {
        var rec = update.ValueKind == JsonValueKind.Object ? update : default;
        return new(
            "tool",
            Role: "Tool",
            ToolCallId: StringField(update, "toolCallId") ?? "tool",
            Title: StringField(update, "title"),
            Status: StringField(update, "status"),
            Locations: rec.ValueKind == JsonValueKind.Object && rec.TryGetProperty("locations", out _) ? LocationsOf(update) : null,
            Text: rec.ValueKind == JsonValueKind.Object && rec.TryGetProperty("content", out var content) ? ContentText(content) : null);
    }

    private static IReadOnlyList<AgentPlanEntry> PlanEntries(JsonElement update)
    {
        if (!update.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) return [];
        return entries.EnumerateArray()
            .Select(entry => new AgentPlanEntry(StringField(entry, "content") ?? ContentText(entry), StringField(entry, "status") ?? "pending"))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Content))
            .ToArray();
    }

    private static IReadOnlyList<AgentPermissionChoice> PermissionOptions(JsonElement request)
    {
        if (!request.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array) return [];
        return options.EnumerateArray()
            .Select(option => new AgentPermissionChoice(StringField(option, "optionId") ?? "", StringField(option, "name") ?? "", StringField(option, "kind")))
            .Where(option => option.OptionId.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> LocationsOf(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array) return [];
        return locations.EnumerateArray().Select(entry => StringField(entry, "path")).OfType<string>().ToArray();
    }

    private static string? FormatUsage(JsonElement update)
    {
        if (!TryNumber(update, "used", out var used) || !TryNumber(update, "size", out var size)) return null;
        return $"{FormatTokens(used)} / {FormatTokens(size)}";
    }

    private static string FormatTokens(double value)
    {
        if (value >= 1_000_000) return (value / 1_000_000).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        if (value >= 10_000) return Math.Round(value / 1000).ToString("0", CultureInfo.InvariantCulture) + "k";
        if (value >= 1000) return (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k";
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string ContentText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Array => string.Join("\n", value.EnumerateArray().Select(ContentText).Where(text => text.Length > 0)),
        JsonValueKind.Object when value.TryGetProperty("text", out var text) => ContentText(text),
        JsonValueKind.Object when value.TryGetProperty("content", out var content) => ContentText(content),
        JsonValueKind.Object when value.TryGetProperty("message", out var message) => ContentText(message),
        _ => ""
    };

    private static string StatusMark(string status) => status == "completed" ? "✓" : status == "in_progress" ? "●" : "○";

    private static JsonElement? ObjectProperty(JsonElement? value, string name)
    {
        if (value is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(name, out var found) || found.ValueKind != JsonValueKind.Object) return null;
        return found;
    }

    private static string? StringField(JsonElement? value, string name)
    {
        if (value is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(name, out var found) || found.ValueKind != JsonValueKind.String) return null;
        var text = found.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryNumber(JsonElement value, string name, out double number)
    {
        number = 0;
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var found) || found.ValueKind != JsonValueKind.Number) return false;
        number = found.GetDouble();
        return true;
    }
}
