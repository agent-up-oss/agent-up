using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentUp.Desktop.Features.Agents.Models;

namespace AgentUp.Desktop.Features.Agents.Providers;

/// <summary>
/// Everything an agent reported about one tool call, accumulated across its updates.
/// <see cref="Name"/> is the first title the call carried, which is the tool's own name; later
/// updates often retitle the call with the path it touched, which <see cref="Title"/> keeps.
/// </summary>
public sealed record AgentToolCallFacts(
    string? Name,
    string? Title,
    string? Kind,
    string? RawInput,
    string? Output,
    string? Status,
    string? RawOutput = null,
    string? UnifiedDiff = null,
    string? DiffPath = null,
    string? DiffOldText = null,
    string? DiffNewText = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null);

/// <summary>How a tool call reads in the transcript, derived from what the agent reported.</summary>
/// <param name="Title">The tool's readable name: "List workspaces", "Read", "Run".</param>
/// <param name="Source">Where the tool came from when that is not the agent itself, such as "Agent-Up".</param>
/// <param name="Summary">The one argument that says what the call was about, kept to one line.</param>
/// <param name="State">"running", "done", or "failed".</param>
/// <param name="Input">The full input, formatted, or null when there was none.</param>
/// <param name="Output">The output's first lines, formatted, or null before there is any.</param>
/// <param name="HiddenLines">How many output lines the preview leaves out.</param>
/// <param name="Diff">What an edit or a new file changed, when the agent said.</param>
/// <param name="Duration">How long the call took, once it took long enough to be worth saying.</param>
public sealed record AgentToolCallView(
    string Title,
    string? Source,
    string? Summary,
    string State,
    string? Input,
    string? Output,
    int HiddenLines,
    string? FullOutput,
    AgentDiff? Diff = null,
    string? Duration = null);

public static partial class AgentToolCallProvider
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
        "command", "filePath", "file_path", "pattern", "query", "path", "url", "description",
        "workspaceId", "worktreePath", "name", "id"
    ];

    // Agent-Up's own MCP servers announce themselves under this prefix.
    private const string AgentUpPrefix = "agent-up-";

    public static AgentToolCallView Present(string? title, string? kind, string? rawInput, string? output, string? status) =>
        Present(new AgentToolCallFacts(title, title, kind, rawInput, output, status));

    public static AgentToolCallView Present(AgentToolCallFacts facts)
    {
        var input = ParseObject(facts.RawInput);
        var (name, source, server) = Name(facts.Name ?? facts.Title, facts.Kind);
        if (name == "Edit" && IsWrite(input))
            name = "Write";
        var summary = Summary(input) ?? PathLikeTitle(facts.Title, facts.Kind) ?? FileLikeTitle(facts.Title, facts.Name) ?? server;
        // Some tools put what they read in their raw output and leave the visible content empty.
        var formatted = Format(string.IsNullOrWhiteSpace(facts.Output) ? facts.RawOutput : facts.Output);
        var lines = formatted?.Split('\n') ?? [];
        var folds = lines.Length - PreviewLines >= MinimumFold;
        var preview = folds ? string.Join('\n', lines.Take(PreviewLines)) : formatted;
        return new AgentToolCallView(
            name,
            source,
            summary is null ? null : Shorten(summary),
            State(facts.Status),
            input is { } value && value.EnumerateObject().Any() ? JsonSerializer.Serialize(value, Pretty) : null,
            string.IsNullOrWhiteSpace(preview) ? null : preview,
            folds ? lines.Length - PreviewLines : 0,
            string.IsNullOrWhiteSpace(formatted) ? null : formatted,
            Diff(facts, input),
            Duration(facts.StartedAt, facts.EndedAt));
    }

    /// <summary>
    /// The edit's diff from the richest source the agent gave: a unified diff, then the old and
    /// new text, then a new file's content.
    /// </summary>
    public static AgentDiff? Diff(AgentToolCallFacts facts, JsonElement? input)
    {
        if (!string.IsNullOrWhiteSpace(facts.UnifiedDiff))
        {
            var unified = AgentDiffProvider.FromUnified(facts.UnifiedDiff, facts.DiffPath);
            if (unified.Lines.Count > 0)
                return unified with { Path = facts.DiffPath ?? Summary(input) ?? unified.Path };
        }

        if (facts.DiffNewText is not null)
            return AgentDiffProvider.FromTexts(facts.DiffOldText, facts.DiffNewText, facts.DiffPath);
        if (IsWrite(input) && input!.Value.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            return AgentDiffProvider.NewFile(content.GetString() ?? "", Summary(input));
        return null;
    }

    /// <summary>A step is only worth timing once it took a second; quick tools say nothing.</summary>
    public static string? Duration(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
    {
        if (startedAt is not { } start || endedAt is not { } end)
            return null;
        var elapsed = end - start;
        if (elapsed < TimeSpan.FromSeconds(1))
            return null;
        return elapsed < TimeSpan.FromMinutes(1)
            ? $"{elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s"
            : $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    }

    // A write carries the whole new content and no text to replace.
    private static bool IsWrite(JsonElement? input) =>
        input is { } value
        && value.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
        && !value.TryGetProperty("oldString", out _) && !value.TryGetProperty("old_string", out _);

    // A completed call is often retitled with the file it touched, when the tool name was something else.
    private static string? FileLikeTitle(string? title, string? name) =>
        title is not null && name is not null && !string.Equals(title, name, StringComparison.Ordinal) && title.Contains('.') && !title.Contains(' ')
            ? title
            : null;

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
        // OpenCode wraps what a read returned in tags; the reader wants the file text.
        if (ReadWrapperRegex().Match(trimmed) is { Success: true } read)
            return string.IsNullOrWhiteSpace(read.Groups["body"].Value) ? null : read.Groups["body"].Value.Trim('\n');
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
            .FirstOrDefault(IsTelling);
        return named ?? value.EnumerateObject().Select(property => Scalar(property.Value)).FirstOrDefault(text => text is { Length: > 0 });
    }

    // "." or "./" says nothing about a call, so a search names its pattern instead of its directory.
    private static bool IsTelling(string? text) => text is { Length: > 0 } and not "." and not "./";

    [GeneratedRegex(@"^<path>[^<]*</path>\s*(?:<type>[^<]*</type>\s*)?<content>(?<body>[\s\S]*)</content>$")]
    private static partial Regex ReadWrapperRegex();

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
