using System.Globalization;
using System.Text.RegularExpressions;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Git.Providers;

public static class GitFileViewerProvider
{
    public const double LineHeight = 22;
    private static readonly Regex HunkHeader = new(@"^@@\s+-(\d+)(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s@@", RegexOptions.CultureInvariant);

    public static IReadOnlyList<GitFileViewerLine> ParseDiff(string? diff)
    {
        if (string.IsNullOrEmpty(diff))
            return [];

        var raw = SplitDiffLines(diff);
        var lines = new List<GitFileViewerLine>(raw.Count);
        var oldNumber = 0;
        var newNumber = 0;
        foreach (var text in raw)
        {
            var index = lines.Count;
            if (text.StartsWith("@@", StringComparison.Ordinal))
            {
                var match = HunkHeader.Match(text);
                if (match.Success)
                {
                    oldNumber = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    newNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                }

                lines.Add(new GitFileViewerLine(index, "hunk", null, null, " ", text));
                continue;
            }

            if (IsMeta(text))
            {
                lines.Add(new GitFileViewerLine(index, "meta", null, null, " ", text));
                continue;
            }

            if (text.StartsWith('+'))
            {
                lines.Add(new GitFileViewerLine(index, "added", null, newNumber, "+", text[1..]));
                newNumber += 1;
                continue;
            }

            if (text.StartsWith('-'))
            {
                lines.Add(new GitFileViewerLine(index, "deleted", oldNumber, null, "−", text[1..]));
                oldNumber += 1;
                continue;
            }

            var body = text.StartsWith(' ') ? text[1..] : text;
            lines.Add(new GitFileViewerLine(index, "context", oldNumber, newNumber, " ", body));
            oldNumber += 1;
            newNumber += 1;
        }

        return lines;
    }

    public static IReadOnlyList<GitFileViewerHunk> Hunks(IReadOnlyList<GitFileViewerLine> lines)
        => lines.Where(line => line.Kind == "hunk")
            .Select(line => new GitFileViewerHunk(line.Text, line.Index))
            .ToList();

    public static IReadOnlyList<string> SplitDiffLines(string diff)
    {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < diff.Length; index++)
        {
            if (diff[index] != '\n')
                continue;
            var end = index > start && diff[index - 1] == '\r' ? index - 1 : index;
            lines.Add(diff[start..end]);
            start = index + 1;
        }

        if (start < diff.Length)
            lines.Add(diff[start..]);
        return lines;
    }

    public static string DetectLanguage(string path) => SyntaxHighlightProvider.DetectLanguage(path);

    public static IReadOnlyList<GitFileViewerToken> Highlight(string path, GitFileViewerLine line)
    {
        if (line.Kind is "hunk" or "meta")
            return [new GitFileViewerToken("plain", line.Text)];
        return Tokenize(line.Text, DetectLanguage(path));
    }

    public static int? JumpIndex(IReadOnlyList<GitFileViewerLine> lines, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return null;
        if (int.TryParse(trimmed, CultureInfo.InvariantCulture, out var number) && number.ToString(CultureInfo.InvariantCulture) == trimmed)
        {
            var match = lines.FirstOrDefault(line => line.NewNumber == number || line.OldNumber == number);
            return match?.Index;
        }

        var hunk = lines.FirstOrDefault(line => line.Kind == "hunk" && line.Text.Contains(trimmed, StringComparison.Ordinal));
        return hunk?.Index;
    }

    public static IReadOnlyList<GitFileViewerToken> Tokenize(string text, string language)
        => SyntaxHighlightProvider.Tokenize(text, language).Select(token => new GitFileViewerToken(token.Kind, token.Text)).ToArray();

    private static bool IsMeta(string text)
        => text.StartsWith("diff --git", StringComparison.Ordinal)
           || text.StartsWith("index ", StringComparison.Ordinal)
           || text.StartsWith("--- ", StringComparison.Ordinal)
           || text.StartsWith("+++ ", StringComparison.Ordinal)
           || text.StartsWith("new file", StringComparison.Ordinal)
           || text.StartsWith("deleted file", StringComparison.Ordinal)
           || text.StartsWith("similarity index", StringComparison.Ordinal)
           || text.StartsWith("rename ", StringComparison.Ordinal)
           || text.StartsWith("\\ ", StringComparison.Ordinal);

}

public sealed record GitFileViewerLine(
    int Index,
    string Kind,
    int? OldNumber,
    int? NewNumber,
    string Prefix,
    string Text);

public sealed record GitFileViewerToken(string Kind, string Text);

public sealed record GitFileViewerHunk(string Label, int LineIndex);
