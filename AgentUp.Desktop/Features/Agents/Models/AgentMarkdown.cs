namespace AgentUp.Desktop.Features.Agents.Models;

/// <summary>What one markdown block in an agent reply is.</summary>
public enum AgentMarkdownBlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Quote,
    Code,
    Rule
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
/// <see cref="Language"/> belong to a fenced code block.
/// </summary>
public sealed record AgentMarkdownBlock(
    AgentMarkdownBlockKind Kind,
    IReadOnlyList<AgentMarkdownInline> Inlines,
    int Level = 0,
    string? Marker = null,
    string? Code = null,
    string? Language = null);
