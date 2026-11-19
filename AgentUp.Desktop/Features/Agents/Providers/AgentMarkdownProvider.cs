using System.Text;
using System.Text.RegularExpressions;
using AgentUp.Desktop.Features.Agents.Models;

namespace AgentUp.Desktop.Features.Agents.Providers;

/// <summary>
/// Reads the markdown an agent writes into blocks and inline runs the transcript can draw.
/// It covers what coding agents actually emit - headings, paragraphs, bullet and numbered
/// lists, quotes, rules, fenced code, inline code, emphasis and links - and it is tolerant of a
/// reply that is still streaming: an unclosed fence is drawn as code so far, and unmatched
/// emphasis markers stay literal text rather than swallowing the rest of the reply.
/// </summary>
public static partial class AgentMarkdownProvider
{
    public static IReadOnlyList<AgentMarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<AgentMarkdownBlock>();
        if (string.IsNullOrWhiteSpace(markdown))
            return blocks;

        var lines = Unwrapped(markdown.Replace("\r\n", "\n", StringComparison.Ordinal)).Split('\n');
        var paragraph = new List<string>();
        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new(AgentMarkdownBlockKind.Paragraph, Inlines(string.Join(" ", paragraph))));
            paragraph.Clear();
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var fence = FenceRegex().Match(trimmed);
            if (fence.Success)
            {
                FlushParagraph();
                var marker = fence.Groups["fence"].Value;
                var language = fence.Groups["lang"].Value.Trim();
                // The block runs to the closing fence, or to the end of a reply still streaming.
                var close = index + 1;
                while (close < lines.Length && !lines[close].Trim().StartsWith(marker, StringComparison.Ordinal))
                    close++;
                var code = lines[(index + 1)..close];
                index = close;
                blocks.Add(new(AgentMarkdownBlockKind.Code, [], Code: string.Join("\n", code).TrimEnd(), Language: language.Length == 0 ? null : language));
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (trimmed.Contains('|') && index + 1 < lines.Length && TableSeparatorRegex().IsMatch(lines[index + 1].Trim()))
            {
                FlushParagraph();
                var header = Cells(trimmed);
                var alignments = Cells(lines[index + 1].Trim()).Select(Alignment).ToArray();
                var rows = new List<IReadOnlyList<IReadOnlyList<AgentMarkdownInline>>> { header.Select(Inlines).ToArray() };
                // Body rows follow the separator until a line without a pipe, or a blank one.
                var end = index + 2;
                while (end < lines.Length && lines[end].Contains('|') && lines[end].Trim().Length > 0)
                    end++;
                rows.AddRange(lines[(index + 2)..end].Select(line => Cells(line.Trim()))
                    .Select(cells => (IReadOnlyList<IReadOnlyList<AgentMarkdownInline>>)Enumerable.Range(0, header.Count).Select(column => Inlines(column < cells.Count ? cells[column] : "")).ToArray()));
                index = end - 1;
                blocks.Add(new(AgentMarkdownBlockKind.Table, [], Rows: rows, Alignments: alignments.Concat(Enumerable.Repeat("left", Math.Max(0, header.Count - alignments.Length))).Take(header.Count).ToArray()));
                continue;
            }

            if (RuleRegex().IsMatch(trimmed))
            {
                FlushParagraph();
                blocks.Add(new(AgentMarkdownBlockKind.Rule, []));
                continue;
            }

            var heading = HeadingRegex().Match(trimmed);
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new(AgentMarkdownBlockKind.Heading, Inlines(heading.Groups["text"].Value.TrimEnd('#', ' ')), Level: heading.Groups["hashes"].Length));
                continue;
            }

            var item = ListRegex().Match(line);
            if (item.Success)
            {
                FlushParagraph();
                var depth = item.Groups["indent"].Value.Replace("\t", "  ", StringComparison.Ordinal).Length / 2;
                var bullet = item.Groups["bullet"].Value;
                var marker = char.IsDigit(bullet[0]) ? bullet.TrimEnd(')', '.') + "." : "•";
                blocks.Add(new(AgentMarkdownBlockKind.ListItem, Inlines(item.Groups["text"].Value), Level: depth, Marker: marker));
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                var quote = new StringBuilder(trimmed.TrimStart('>').Trim());
                while (index + 1 < lines.Length && lines[index + 1].TrimStart().StartsWith('>'))
                    quote.Append(' ').Append(lines[++index].Trim().TrimStart('>').Trim());
                blocks.Add(new(AgentMarkdownBlockKind.Quote, Inlines(quote.ToString())));
                continue;
            }

            // A list item continues onto an indented line that is not a new item.
            if (paragraph.Count == 0 && blocks.Count > 0 && blocks[^1].Kind == AgentMarkdownBlockKind.ListItem && line.StartsWith("  ", StringComparison.Ordinal))
            {
                var previous = blocks[^1];
                blocks[^1] = previous with { Inlines = [.. previous.Inlines, new(AgentMarkdownInlineKind.Text, " "), .. Inlines(trimmed)] };
                continue;
            }

            paragraph.Add(trimmed);
        }

        FlushParagraph();
        return blocks;
    }

    /// <summary>
    /// A reply that is nothing but one <c>markdown</c> or <c>md</c> fence is markdown the model
    /// wrapped by habit, not a code sample: it is drawn as the markdown inside. While it is still
    /// streaming the closing fence has not arrived yet, so an open wrapper is unwrapped too.
    /// </summary>
    public static string Unwrapped(string markdown)
    {
        var trimmed = markdown.Trim();
        var open = WrapperRegex().Match(trimmed);
        if (!open.Success)
            return markdown;
        var body = trimmed[open.Length..];
        var marker = open.Groups["fence"].Value;
        var close = body.LastIndexOf("\n" + marker, StringComparison.Ordinal);
        if (close < 0)
            return body.Contains("\n" + marker[0], StringComparison.Ordinal) ? markdown : body;
        // Only a closing fence at the very end makes the whole reply one wrapper.
        return body[(close + 1 + marker.Length)..].Trim().Length == 0 && !body[..close].Contains("\n" + marker, StringComparison.Ordinal)
            ? body[..close]
            : markdown;
    }

    /// <summary>A table row's cells, without the optional outer pipes; <c>\|</c> stays a literal pipe.</summary>
    private static IReadOnlyList<string> Cells(string row)
    {
        var trimmed = row.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|') && !trimmed.EndsWith("\\|", StringComparison.Ordinal)) trimmed = trimmed[..^1];
        var cells = new List<string>();
        var current = new StringBuilder();
        for (var position = 0; position < trimmed.Length; position++)
        {
            if (trimmed[position] == '\\' && position + 1 < trimmed.Length && trimmed[position + 1] == '|')
            {
                current.Append('|');
                position++;
                continue;
            }

            if (trimmed[position] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(trimmed[position]);
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static string Alignment(string separator) => separator switch
    {
        _ when separator.StartsWith(':') && separator.EndsWith(':') => "center",
        _ when separator.EndsWith(':') => "right",
        _ => "left"
    };

    /// <summary>Splits one block of text into plain, bold, italic, code and link runs.</summary>
    public static IReadOnlyList<AgentMarkdownInline> Inlines(string text)
    {
        var runs = new List<AgentMarkdownInline>();
        var plain = new StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            runs.Add(new(AgentMarkdownInlineKind.Text, plain.ToString()));
            plain.Clear();
        }

        var position = 0;
        while (position < text.Length)
        {
            if (text[position] == '`')
            {
                var ticks = CountRun(text, position, '`');
                var close = text.IndexOf(new string('`', ticks), position + ticks, StringComparison.Ordinal);
                if (close > position)
                {
                    Flush();
                    runs.Add(new(AgentMarkdownInlineKind.Code, text[(position + ticks)..close].Trim()));
                    position = close + ticks;
                    continue;
                }
            }
            else if (text[position] == '[' && LinkRegex().Match(text, position) is { Success: true, Index: var at } link && at == position)
            {
                Flush();
                runs.Add(new(AgentMarkdownInlineKind.Link, link.Groups["text"].Value, link.Groups["url"].Value));
                position += link.Length;
                continue;
            }
            else if (TryEmphasis(text, position, out var kind, out var width, out var close))
            {
                Flush();
                runs.Add(new(kind, text[(position + width)..close]));
                position = close + width;
                continue;
            }

            plain.Append(text[position]);
            position++;
        }

        Flush();
        return runs;
    }

    private static bool TryEmphasis(string text, int position, out AgentMarkdownInlineKind kind, out int width, out int close)
    {
        foreach (var (marker, emphasis) in new[] { ("**", AgentMarkdownInlineKind.Bold), ("__", AgentMarkdownInlineKind.Bold), ("*", AgentMarkdownInlineKind.Italic), ("_", AgentMarkdownInlineKind.Italic) })
        {
            if (marker[0] == '_' && !IsWordBoundary(text, position))
                continue;
            if (Emphasis(text, position, marker) is not { } found)
                continue;
            kind = emphasis;
            width = marker.Length;
            close = found;
            return true;
        }

        kind = AgentMarkdownInlineKind.Text;
        width = 0;
        close = -1;
        return false;
    }

    /// <summary>The index of the closing marker for emphasis opening at <paramref name="position"/>, or null.</summary>
    private static int? Emphasis(string text, int position, string marker)
    {
        if (string.CompareOrdinal(text, position, marker, 0, marker.Length) != 0)
            return null;
        var start = position + marker.Length;
        if (start >= text.Length || char.IsWhiteSpace(text[start]) || text[start] == marker[0])
            return null;
        var close = text.IndexOf(marker, start, StringComparison.Ordinal);
        while (close > start && char.IsWhiteSpace(text[close - 1]))
            close = text.IndexOf(marker, close + marker.Length, StringComparison.Ordinal);
        if (close <= start)
            return null;
        // Underscore emphasis has to close at a word boundary, so snake_case names stay intact.
        if (marker[0] == '_' && close + marker.Length < text.Length && char.IsLetterOrDigit(text[close + marker.Length]))
            return null;
        return close;
    }

    private static bool IsWordBoundary(string text, int position) => position == 0 || !char.IsLetterOrDigit(text[position - 1]);

    private static int CountRun(string text, int position, char value)
    {
        var count = 0;
        while (position + count < text.Length && text[position + count] == value)
            count++;
        return count;
    }

    [GeneratedRegex(@"^(?<fence>`{3,}|~{3,})(?<lang>[^`]*)$")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^(?<fence>`{3,}|~{3,})[ \t]*(?:markdown|md)[ \t]*\n", RegexOptions.IgnoreCase)]
    private static partial Regex WrapperRegex();

    // GitHub-flavoured markdown needs one hyphen per separator cell; ":-:" centres a column.
    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(?:\|\s*:?-+:?\s*)*\|?$")]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"^(?:-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<text>.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<bullet>[-*+]|\d{1,3}[.)])\s+(?<text>.*)$")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)\s]+)\)")]
    private static partial Regex LinkRegex();
}
