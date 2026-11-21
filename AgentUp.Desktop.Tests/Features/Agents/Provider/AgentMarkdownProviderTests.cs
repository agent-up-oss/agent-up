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

    [Test]
    public void A_table_keeps_its_header_rows_alignments_and_escaped_pipes()
    {
        var blocks = AgentMarkdownProvider.Parse("Ports:\n| Name | Port | Note |\n|:---|---:|:---:|\n| web | 3000 | a \\| b |\n| api |\n\nAfter.");

        var table = blocks[1];
        Assert.That(blocks.Select(block => block.Kind), Is.EqualTo(new[] { AgentMarkdownBlockKind.Paragraph, AgentMarkdownBlockKind.Table, AgentMarkdownBlockKind.Paragraph }));
        Assert.That(table.Alignments, Is.EqualTo(new[] { "left", "right", "center" }));
        Assert.That(table.Rows!.Select(row => row.Count), Is.All.EqualTo(3), "A short row is padded to the header.");
        Assert.That(string.Concat(table.Rows![1][2].Select(inline => inline.Text)), Is.EqualTo("a | b"));
    }

    [Test]
    public void A_reply_wrapped_whole_in_a_markdown_fence_is_drawn_as_its_markdown()
    {
        var closed = AgentMarkdownProvider.Parse("```markdown\n# Title\n\n| A | B |\n|---|---|\n| 1 | 2 |\n```");
        var streaming = AgentMarkdownProvider.Parse("```md\n# Title\n\nStill wri");
        var sample = AgentMarkdownProvider.Parse("Here is markdown:\n```markdown\n# Title\n```");
        var two = AgentMarkdownProvider.Parse("```markdown\n# A\n```\n\n```markdown\n# B\n```");

        Assert.That(closed.Select(block => block.Kind), Is.EqualTo(new[] { AgentMarkdownBlockKind.Heading, AgentMarkdownBlockKind.Table }));
        Assert.That(streaming.Select(block => block.Kind), Is.EqualTo(new[] { AgentMarkdownBlockKind.Heading, AgentMarkdownBlockKind.Paragraph }));
        Assert.That(sample[1].Kind, Is.EqualTo(AgentMarkdownBlockKind.Code), "A fence inside prose is a code sample.");
        Assert.That(two.Select(block => block.Kind), Is.All.EqualTo(AgentMarkdownBlockKind.Code), "Two fences are two samples, not one wrapper.");
    }

    [Test]
    public void Block_keys_change_only_when_what_the_block_draws_changes()
    {
        var before = AgentMarkdownProvider.Parse("# T\n\nOne");
        var after = AgentMarkdownProvider.Parse("# T\n\nOne two");

        Assert.That(after[0].Key, Is.EqualTo(before[0].Key));
        Assert.That(after[1].Key, Is.Not.EqualTo(before[1].Key));
    }

    private static string Plain(AgentMarkdownBlock block) => string.Concat(block.Inlines.Select(inline => inline.Text));
}
