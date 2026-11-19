using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AgentUp.Desktop.Features.Agents.Providers;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Agents.Views;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

/// <summary>Highlighted code, tables, incremental streaming, edit diffs and grouped reads as drawn.</summary>
[TestFixture]
public sealed class AgentTranscriptBlockTests
{
    [AvaloniaTest]
    public void Code_in_a_known_language_is_highlighted_and_unknown_code_stays_plain()
    {
        var view = new AgentMarkdownView { Markdown = "```ts\nconst port = 3001;\n```\n\n```\nplain words\n```" };

        var highlighted = (StackPanel)CodeContent(view.Children[0]);
        var unlabelled = (Panel)((Border)view.Children[1]).Child!;
        var plain = ((ScrollViewer)unlabelled.Children[0]).Content!;
        var classes = ((AgentSyntaxLineView)highlighted.Children[0]).Children.SelectMany(token => token.Classes).ToArray();

        Assert.That(classes, Does.Contain("au-syntax-keyword"));
        Assert.That(classes, Does.Contain("au-syntax-number"));
        Assert.That(plain, Is.TypeOf<TextBlock>());
        Assert.That(((TextBlock)plain).Text, Is.EqualTo("plain words"));
        Assert.That(((Button)unlabelled.Children[1]).Content, Is.EqualTo("Copy"), "Unlabelled code keeps Copy, in the corner.");
    }

    [AvaloniaTest]
    public void A_table_draws_a_header_band_rows_and_aligned_cells()
    {
        var view = new AgentMarkdownView { Markdown = "| Name | Port |\n|---|---:|\n| web | 3000 |\n| api | 5000 |" };

        var table = (Border)view.Children.Single();
        var grid = (Grid)((ScrollViewer)table.Child!).Content!;
        var cells = grid.Children.OfType<TextBlock>().ToArray();

        Assert.That(table.Classes, Does.Contain("au-md-table"));
        Assert.That(grid.ColumnDefinitions, Has.Count.EqualTo(2));
        Assert.That(grid.Children.OfType<Border>().Select(band => band.Classes.Single()), Is.EqualTo(new[] { "au-md-table-head", "au-md-table-row", "au-md-table-row" }));
        Assert.That(cells.Select(cell => cell.Inlines!.OfType<Run>().Single().Text), Is.EqualTo(new[] { "Name", "Port", "web", "3000", "api", "5000" }));
        Assert.That(cells[0].Classes, Does.Contain("au-md-table-header-cell"));
        Assert.That(cells[3].TextAlignment, Is.EqualTo(TextAlignment.Right));
    }

    [AvaloniaTest]
    public void A_streaming_reply_rebuilds_only_the_blocks_a_chunk_changed()
    {
        var view = new AgentMarkdownView { Markdown = "# Title\n\nFirst paragraph.\n\nSec" };
        var heading = view.Children[0];
        var first = view.Children[1];

        view.Markdown = "# Title\n\nFirst paragraph.\n\nSecond paragraph grows";

        Assert.That(view.LastRebuilt, Is.EqualTo(1));
        Assert.That(view.Children[0], Is.SameAs(heading));
        Assert.That(view.Children[1], Is.SameAs(first));
        Assert.That(view.Children, Has.Count.EqualTo(3));
    }

    [AvaloniaTest]
    public async Task An_edit_opens_to_its_diff_and_a_long_diff_folds()
    {
        var item = new AgentChatItemViewModel("Tool", "edit", "pending", "call-9");
        var unified = "--- a/app.ts\n+++ b/app.ts\n@@ -1,60 +1,60 @@\n" + string.Join('\n', Enumerable.Range(1, 60).Select(index => $"-old {index}\n+new {index}"));
        item.SetTool(new PresentedAgentUpdate("tool", Title: "edit", ToolKind: "edit", Status: "completed", ToolCallId: "call-9", RawInput: """{"filePath":"/repo/app.ts"}""", Text: "Edit applied successfully.", UnifiedDiff: unified), DateTimeOffset.UnixEpoch);

        await item.ToggleToolCommand.Execute().FirstAsync();

        Assert.That(item.HasToolDiffOpen && !item.HasToolOutput && !item.HasToolInput, Is.True);
        Assert.That((item.DiffAdded, item.DiffRemoved), Is.EqualTo(("+60", "−60")));
        Assert.That(item.DiffLines, Has.Count.EqualTo(40));
        Assert.That(item.DiffLines[0].IsHunk && item.DiffLines[1].IsDeleted && item.DiffLines[2].IsAdded, Is.True);
        Assert.That(item.CanShowWholeDiff, Is.True);
        await item.ShowWholeDiffCommand.Execute().FirstAsync();
        Assert.That(item.DiffLines, Has.Count.EqualTo(121));
    }

    [AvaloniaTest]
    public void Consecutive_finished_reads_fold_into_one_row()
    {
        var run = Tool("b", "bash", "execute", """{"command":"ls"}""");

        var entries = AgentToolGroupViewModel.Group([Tool("r1", "read", "read", """{"filePath":"a.ts"}"""), Tool("r2", "read", "read", """{"filePath":"b.ts"}"""), run, Tool("r3", "read", "read", """{"filePath":"c.ts"}""")]);

        var group = (AgentToolGroupViewModel)entries[0];
        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(group.Title, Is.EqualTo("Read 2 files"));
        Assert.That(group.Summary, Is.EqualTo("a.ts, b.ts"));
        Assert.That(entries[1], Is.SameAs(run));
        Assert.That(entries[2], Is.TypeOf<AgentChatItemViewModel>(), "A single read stays an ordinary row.");
    }

    [AvaloniaTest]
    public async Task A_folded_group_opens_to_its_calls_and_searches_read_as_searches()
    {
        var entries = AgentToolGroupViewModel.Group([Tool("s1", "glob", "search", """{"pattern":"*.md"}"""), Tool("s2", "grep", "search", """{"pattern":"port"}""")]);
        var group = (AgentToolGroupViewModel)entries.Single();

        Assert.That(group.Title, Is.EqualTo("Searched 2 times"));
        Assert.That((group.IsOpen, group.Chevron), Is.EqualTo((false, "›")));
        Assert.That(group.AutomationId, Does.Contain("s1"));
        await group.ToggleCommand.Execute().FirstAsync();
        Assert.That((group.IsOpen, group.Chevron), Is.EqualTo((true, "⌄")));
        Assert.That(group.Items.Select(item => item.Tool.Summary), Is.EqualTo(new[] { "*.md", "port" }));
    }

    [AvaloniaTest]
    public void A_running_or_failed_read_is_not_folded_into_a_group()
    {
        var running = new AgentChatItemViewModel("Tool", "read", "pending", "r1");
        running.SetTool("read", "read", """{"filePath":"a.ts"}""", null, "in_progress");

        var entries = AgentToolGroupViewModel.Group([running, Tool("r2", "read", "read", """{"filePath":"b.ts"}""")]);

        Assert.That(entries.OfType<AgentToolGroupViewModel>(), Is.Empty);
        Assert.That(entries, Has.Count.EqualTo(2));
    }

    [AvaloniaTest]
    public void Quotes_rules_nested_lists_emphasis_links_and_centred_columns_draw_with_their_classes()
    {
        var view = new AgentMarkdownView { Markdown = "> quoted\n\n---\n\n- outer\n  - inner *soft* [docs](https://example.com)\n\n| a | b |\n|:-:|---|\n| 1 | 2 |" };

        var quote = (Border)view.Children[0];
        var rule = (Border)view.Children[1];
        var inner = (Border)view.Children[3];
        var runs = ((TextBlock)((Grid)inner.Child!).Children[1]).Inlines!.OfType<Run>().ToArray();
        var table = (Grid)((ScrollViewer)((Border)view.Children[4]).Child!).Content!;

        Assert.That(quote.Classes, Does.Contain("au-md-quote"));
        Assert.That(rule.Classes, Does.Contain("au-md-rule"));
        Assert.That(inner.Margin.Left, Is.GreaterThan(((Border)view.Children[2]).Margin.Left), "A nested item is indented further.");
        Assert.That(runs.Single(run => run.Text == "soft").FontStyle, Is.EqualTo(FontStyle.Italic));
        Assert.That(runs.Single(run => run.Text == "docs").TextDecorations, Is.EqualTo(TextDecorations.Underline));
        Assert.That(table.Children.OfType<TextBlock>().First().TextAlignment, Is.EqualTo(TextAlignment.Center));
    }

    private static AgentChatItemViewModel Tool(string id, string name, string kind, string input)
    {
        var item = new AgentChatItemViewModel("Tool", name, "pending", id);
        item.SetTool(name, kind, input, "x", "completed");
        return item;
    }

    private static object CodeContent(Control block) =>
        ((ScrollViewer)((StackPanel)((Border)block).Child!).Children[1]).Content!;
}
