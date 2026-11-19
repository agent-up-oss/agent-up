namespace AgentUp.Desktop.Features.Agents.Models;

/// <summary>What one markdown block in an agent reply is.</summary>
public enum AgentMarkdownBlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Quote,
    Code,
    Rule,
    Table
}

/// <summary>What one run of text inside a block is.</summary>
public enum AgentMarkdownInlineKind
{
    Text,
    Bold,
    Italic,
    Code,
    Link
}

/// <summary>A run of inline text: plain, emphasised, code, or a link with its target.</summary>
public sealed record AgentMarkdownInline(AgentMarkdownInlineKind Kind, string Text, string? Url = null);

/// <summary>
/// One block of a rendered reply. <see cref="Level"/> is the heading level or the list depth;
/// <see cref="Marker"/> is the list bullet or number; <see cref="Code"/> and
/// <see cref="Language"/> belong to a fenced code block. A table keeps its header as the first
/// of <see cref="Rows"/>, and <see cref="Alignments"/> holds "left", "center", or "right" per column.
/// </summary>
public sealed record AgentMarkdownBlock(
    AgentMarkdownBlockKind Kind,
    IReadOnlyList<AgentMarkdownInline> Inlines,
    int Level = 0,
    string? Marker = null,
    string? Code = null,
    string? Language = null,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<AgentMarkdownInline>>>? Rows = null,
    IReadOnlyList<string>? Alignments = null)
{
    /// <summary>
    /// What this block draws, as text. Two blocks with the same key draw the same thing, which is
    /// how a streaming reply keeps every block that a new chunk did not change.
    /// </summary>
    public string Key => string.Join('\u001f', new[]
    {
        $"{Kind}", Level.ToString(System.Globalization.CultureInfo.InvariantCulture), Marker ?? "", Code ?? "", Language ?? "",
        string.Join('\u001e', Inlines.Select(inline => $"{inline.Kind}:{inline.Text}:{inline.Url}")),
        string.Join('\u001d', (Rows ?? []).Select(row => string.Join('\u001e', row.Select(cell => string.Concat(cell.Select(inline => $"{inline.Kind}:{inline.Text}")))))),
        string.Join(',', Alignments ?? [])
    });
}
