using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.Providers;

/// <summary>How a tool call reads in the transcript, derived from what the agent reported.</summary>
/// <param name="Title">The tool's readable name: "List workspaces", "Read", "Run".</param>
/// <param name="Source">Where the tool came from when that is not the agent itself, such as "Agent-Up".</param>
/// <param name="Summary">The one argument that says what the call was about, kept to one line.</param>
/// <param name="State">"running", "done", or "failed".</param>
/// <param name="Input">The full input, formatted, or null when there was none.</param>
/// <param name="Output">The output's first lines, formatted, or null before there is any.</param>
/// <param name="HiddenLines">How many output lines the preview leaves out.</param>
public sealed record AgentToolCallView(
    string Title,
    string? Source,
    string? Summary,
    string State,
    string? Input,
    string? Output,
    int HiddenLines,
    string? FullOutput);

public static class AgentToolCallProvider
{
    /// <summary>Output lines shown before the rest is folded behind a count.</summary>
    public const int PreviewLines = 14;

    /// <summary>Folding away fewer lines than this costs a click for nothing, so short overflow is shown.</summary>
    public const int MinimumFold = 4;

    private const int SummaryChars = 72;

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // The ACP tool kinds, named the way a person describes the step.
    private static readonly Dictionary<string, string> KindTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["read"] = "Read",
        ["edit"] = "Edit",
        ["delete"] = "Delete",
        ["move"] = "Move",
        ["search"] = "Search",
        ["execute"] = "Run",
        ["think"] = "Think",
        ["fetch"] = "Fetch",
        ["switch_mode"] = "Switch mode"
    };

    // The arguments that say what a call was about, most telling first.
    private static readonly string[] SummaryKeys =
    [
        "command", "filePath", "file_path", "path", "pattern", "query", "url", "description",
        "workspaceId", "worktreePath", "name", "id"
    ];

    // Agent-Up's own MCP servers announce themselves under this prefix.
    private const string AgentUpPrefix = "agent-up-";

    public static AgentToolCallView Present(string? title, string? kind, string? rawInput, string? output, string? status)
    {
        var input = ParseObject(rawInput);
        var (name, source, server) = Name(title, kind);
        var summary = Summary(input) ?? PathLikeTitle(title, kind) ?? server;
        var formatted = Format(output);
        var lines = formatted?.Split('\n') ?? [];
        var folds = lines.Length - PreviewLines >= MinimumFold;
        var preview = folds ? string.Join('\n', lines.Take(PreviewLines)) : formatted;
        return new AgentToolCallView(
            name,
            source,
            summary is null ? null : Shorten(summary),
            State(status),
            input is { } value && value.EnumerateObject().Any() ? JsonSerializer.Serialize(value, Pretty) : null,
            string.IsNullOrWhiteSpace(preview) ? null : preview,
            folds ? lines.Length - PreviewLines : 0,
            string.IsNullOrWhiteSpace(formatted) ? null : formatted);
    }

    /// <summary>
    /// The tool's readable name and its source. An MCP tool arrives as
    /// <c>server_tool</c>; Agent-Up's servers are named <c>agent-up-*</c>.
    /// </summary>
    public static (string Name, string? Source, string? Server) Name(string? title, string? kind)
    {
        var raw = title?.Trim() ?? "";
        var separator = raw.IndexOf('_');
        if (separator > 0 && !raw.Contains(' ') && !raw.Contains('/') && raw[..separator].Contains('-'))
        {
            var server = raw[..separator];
            var tool = Humanize(raw[(separator + 1)..]);
            return server.StartsWith(AgentUpPrefix, StringComparison.Ordinal)
                ? (tool, "Agent-Up", Humanize(server[AgentUpPrefix.Length..]).ToLowerInvariant())
                : (tool, Humanize(server), null);
        }

        if (kind is not null && KindTitles.TryGetValue(kind, out var named) && (raw.Length == 0 || LooksLikePath(raw) || !kind.Equals("other", StringComparison.OrdinalIgnoreCase)))
            return (named, null, null);
        return (raw.Length == 0 ? "Tool" : Humanize(raw), null, null);
    }

    public static string State(string? status) => status switch
    {
        "completed" => "done",
        "failed" => "failed",
        _ => "running"
    };

    /// <summary>JSON output is shown indented; anything else is shown as the agent wrote it.</summary>
    public static string? Format(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;
        var trimmed = output.Trim();
        if (trimmed[0] is '{' or '[')
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                return JsonSerializer.Serialize(document.RootElement, Pretty);
            }
            catch (JsonException)
            {
                return trimmed;
            }
        }

        return trimmed;
    }

    /// <summary>Keeps a long path or command on one line by cutting the middle, where it says least.</summary>
    public static string Shorten(string value)
    {
        var line = value.ReplaceLineEndings(" ").Trim();
        if (line.Length <= SummaryChars)
            return line;
        var keep = (SummaryChars - 1) / 2;
        return string.Concat(line.AsSpan(0, keep), "…", line.AsSpan(line.Length - keep));
    }

    private static string? Summary(JsonElement? input)
    {
        if (input is not { } value)
            return null;
        var named = SummaryKeys
            .Select(key => value.TryGetProperty(key, out var found) ? Scalar(found) : null)
            .FirstOrDefault(text => text is { Length: > 0 });
        return named ?? value.EnumerateObject().Select(property => Scalar(property.Value)).FirstOrDefault(text => text is { Length: > 0 });
    }

    // An agent's own edit or read is often titled with the path it touched.
    private static string? PathLikeTitle(string? title, string? kind) =>
        title is not null && LooksLikePath(title) && kind is not null ? title : null;

    private static bool LooksLikePath(string value) => value.Contains('/') || value.Contains('\\');

    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    private static JsonElement? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary><c>list_workspaces</c> and <c>listWorkspaces</c> both read "List workspaces".</summary>
    private static string Humanize(string name)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var character in name)
        {
            if (character is '_' or '-' or '.' or ' ')
            {
                if (current.Length > 0) words.Add(current.ToString());
                current.Clear();
                continue;
            }

            if (char.IsUpper(character) && current.Length > 0 && !char.IsUpper(current[^1]))
            {
                words.Add(current.ToString());
                current.Clear();
            }

            current.Append(character);
        }

        if (current.Length > 0) words.Add(current.ToString());
        if (words.Count == 0)
            return name;
        var sentence = string.Join(' ', words.Select(word => word.ToLowerInvariant()));
        return char.ToUpper(sentence[0], CultureInfo.InvariantCulture) + sentence[1..];
    }
}
