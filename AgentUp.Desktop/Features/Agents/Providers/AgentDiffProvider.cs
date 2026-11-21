using System.Globalization;
using System.Text.RegularExpressions;
using AgentUp.Desktop.Features.Agents.Models;

namespace AgentUp.Desktop.Features.Agents.Providers;

/// <summary>
/// Turns what a coding agent reports about an edit into diff lines. Agents report edits three
/// ways, so there are three readers: a unified diff (OpenCode's tool metadata), an old and new
/// text pair (ACP's diff content, often only the replaced snippet), and a new file's content.
/// </summary>
public static partial class AgentDiffProvider
{
    /// <summary>Unchanged lines kept around each change when two texts are compared.</summary>
    public const int Context = 3;

    /// <summary>Beyond this many lines a side, two texts are shown as a whole replacement.</summary>
    public const int CompareLimit = 600;

    public static AgentDiff FromUnified(string unified, string? path = null)
    {
        var lines = new List<AgentDiffLine>();
        int oldLine = 0, newLine = 0;
        string? file = path;
        foreach (var raw in unified.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var hunk = HunkRegex().Match(raw);
            if (hunk.Success)
            {
                oldLine = int.Parse(hunk.Groups["old"].Value, CultureInfo.InvariantCulture);
                newLine = int.Parse(hunk.Groups["new"].Value, CultureInfo.InvariantCulture);
                lines.Add(new("hunk", null, null, raw));
                continue;
            }

            if (raw.StartsWith("+++ ", StringComparison.Ordinal))
            {
                file ??= raw[4..].Trim();
                continue;
            }

            if (lines.Count == 0 || IsHeader(raw))
                continue;
            if (raw.StartsWith('+'))
                lines.Add(new("added", null, newLine++, raw[1..]));
            else if (raw.StartsWith('-'))
                lines.Add(new("deleted", oldLine++, null, raw[1..]));
            else if (raw.StartsWith(' '))
                lines.Add(new("context", oldLine++, newLine++, raw[1..]));
        }

        return Count(file, lines);
    }

    /// <summary>A new file: every line was added.</summary>
    public static AgentDiff NewFile(string content, string? path) =>
        Count(path, SplitLines(content).Select((line, index) => new AgentDiffLine("added", null, index + 1, line)).ToArray());

    /// <summary>
    /// The lines that changed between two texts, with a little context around each change. Line
    /// numbers count from the start of the given text, which may be a snippet of the file.
    /// </summary>
    public static AgentDiff FromTexts(string? oldText, string newText, string? path)
    {
        var before = oldText is null ? [] : SplitLines(oldText);
        var after = SplitLines(newText);
        if (before.Length > CompareLimit || after.Length > CompareLimit)
            return Count(path, before.Select((line, index) => new AgentDiffLine("deleted", index + 1, null, line))
                .Concat(after.Select((line, index) => new AgentDiffLine("added", null, index + 1, line))).ToArray());

        var edits = Compare(before, after);
        var keep = new bool[edits.Count];
        for (var index = 0; index < edits.Count; index++)
        {
            if (edits[index].Kind == "context")
                continue;
            for (var near = Math.Max(0, index - Context); near <= Math.Min(edits.Count - 1, index + Context); near++)
                keep[near] = true;
        }

        var lines = new List<AgentDiffLine>();
        for (var index = 0; index < edits.Count; index++)
        {
            if (!keep[index])
                continue;
            if (lines.Count > 0 && !keep[index - 1])
                lines.Add(new("hunk", null, null, "…"));
            lines.Add(edits[index]);
        }

        return Count(path, lines);
    }

    /// <summary>Longest-common-subsequence line diff: deletions before additions within a change.</summary>
    private static List<AgentDiffLine> Compare(string[] before, string[] after)
    {
        var table = new int[before.Length + 1, after.Length + 1];
        for (var left = before.Length - 1; left >= 0; left--)
            for (var right = after.Length - 1; right >= 0; right--)
                table[left, right] = before[left] == after[right]
                    ? table[left + 1, right + 1] + 1
                    : Math.Max(table[left + 1, right], table[left, right + 1]);

        var lines = new List<AgentDiffLine>();
        int i = 0, j = 0;
        while (i < before.Length || j < after.Length)
        {
            if (i < before.Length && j < after.Length && before[i] == after[j])
            {
                lines.Add(new("context", i + 1, j + 1, before[i]));
                i++;
                j++;
            }
            else if (j == after.Length || (i < before.Length && table[i + 1, j] >= table[i, j + 1]))
            {
                lines.Add(new("deleted", i + 1, null, before[i]));
                i++;
            }
            else
            {
                lines.Add(new("added", null, j + 1, after[j]));
                j++;
            }
        }

        return lines;
    }

    private static AgentDiff Count(string? path, IReadOnlyList<AgentDiffLine> lines) =>
        new(path, lines, lines.Count(line => line.Kind == "added"), lines.Count(line => line.Kind == "deleted"));

    private static string[] SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalized.EndsWith('\n'))
            normalized = normalized[..^1];
        return normalized.Length == 0 ? [] : normalized.Split('\n');
    }

    private static bool IsHeader(string line) =>
        line.StartsWith("--- ", StringComparison.Ordinal)
        || line.StartsWith("Index: ", StringComparison.Ordinal)
        || line.StartsWith("====", StringComparison.Ordinal)
        || line.StartsWith("diff --git", StringComparison.Ordinal)
        || line.StartsWith("index ", StringComparison.Ordinal)
        || line.StartsWith("\\ ", StringComparison.Ordinal);

    [GeneratedRegex(@"^@@\s+-(?<old>\d+)(?:,\d+)?\s+\+(?<new>\d+)(?:,\d+)?\s+@@")]
    private static partial Regex HunkRegex();
}
