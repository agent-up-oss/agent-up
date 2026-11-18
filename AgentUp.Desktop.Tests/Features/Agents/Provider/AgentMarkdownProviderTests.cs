using AgentUp.Desktop.Features.Agents.Models;
using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentMarkdownProviderTests
{
    [Test]
    public void Parse_reads_headings_paragraphs_lists_quotes_and_rules()
    {
        var blocks = AgentMarkdownProvider.Parse("""
            ## Workspaces
            One line
            that continues.

            - first
              still first
            2. second
            > quoted
            ---
            """);

        Assert.That(blocks.Select(block => block.Kind), Is.EqualTo(new[]
        {
            AgentMarkdownBlockKind.Heading, AgentMarkdownBlockKind.Paragraph, AgentMarkdownBlockKind.ListItem,
            AgentMarkdownBlockKind.ListItem, AgentMarkdownBlockKind.Quote, AgentMarkdownBlockKind.Rule
        }));
        Assert.That(blocks[0].Level, Is.EqualTo(2));
        Assert.That(Plain(blocks[1]), Is.EqualTo("One line that continues."));
        Assert.That(Plain(blocks[2]), Is.EqualTo("first still first"));
        Assert.That(blocks[2].Marker, Is.EqualTo("•"));
        Assert.That(blocks[3].Marker, Is.EqualTo("2."));
    }

    [Test]
    public void Parse_keeps_fenced_code_verbatim_and_draws_an_unclosed_fence_as_code_so_far()
    {
        var closed = AgentMarkdownProvider.Parse("```json\n{ \"a\": 1 }\n```\nafter");
        var streaming = AgentMarkdownProvider.Parse("Look:\n```bash\nnpm test");

        Assert.That(closed[0].Kind, Is.EqualTo(AgentMarkdownBlockKind.Code));
        Assert.That(closed[0].Language, Is.EqualTo("json"));
        Assert.That(closed[0].Code, Is.EqualTo("{ \"a\": 1 }"));
        Assert.That(Plain(closed[1]), Is.EqualTo("after"));
        Assert.That(streaming[^1].Code, Is.EqualTo("npm test"));
    }

    [Test]
    public void Parse_marks_nested_list_depth()
    {
        var blocks = AgentMarkdownProvider.Parse("- top\n  - nested\n    1) deeper");

        Assert.That(blocks.Select(block => block.Level), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(blocks[2].Marker, Is.EqualTo("1."));
    }

    [Test]
    public void Inlines_reads_code_bold_italic_and_links()
    {
        var inlines = AgentMarkdownProvider.Inlines("Run `npm test` on **main** with *care*, see [docs](https://example.test).");

        Assert.That(inlines.Select(inline => inline.Kind), Is.EqualTo(new[]
        {
            AgentMarkdownInlineKind.Text, AgentMarkdownInlineKind.Code, AgentMarkdownInlineKind.Text, AgentMarkdownInlineKind.Bold,
            AgentMarkdownInlineKind.Text, AgentMarkdownInlineKind.Italic, AgentMarkdownInlineKind.Text, AgentMarkdownInlineKind.Link,
            AgentMarkdownInlineKind.Text
        }));
        Assert.That(inlines[1].Text, Is.EqualTo("npm test"));
        Assert.That(inlines[7].Url, Is.EqualTo("https://example.test"));
    }

    [Test]
    public void Inlines_leaves_snake_case_and_unmatched_markers_as_text()
    {
        var snake = AgentMarkdownProvider.Inlines("call list_workspaces and get_workspace_status");
        var unmatched = AgentMarkdownProvider.Inlines("2 * 3 and **open");

        Assert.That(snake, Has.Count.EqualTo(1));
        Assert.That(snake[0].Text, Is.EqualTo("call list_workspaces and get_workspace_status"));
        Assert.That(unmatched.Single().Text, Is.EqualTo("2 * 3 and **open"));
    }

    [Test]
    public void Parse_returns_nothing_for_an_empty_reply()
    {
        Assert.That(AgentMarkdownProvider.Parse("  \n "), Is.Empty);
        Assert.That(AgentMarkdownProvider.Parse(null), Is.Empty);
    }

    private static string Plain(AgentMarkdownBlock block) => string.Concat(block.Inlines.Select(inline => inline.Text));
}
