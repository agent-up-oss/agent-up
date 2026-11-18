using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Agents.Views;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentTranscriptRenderingTests
{
    [AvaloniaTest]
    public void Markdown_view_draws_catalog_blocks_and_inline_runs()
    {
        var view = new AgentMarkdownView { Markdown = "## Result\nRun `npm test` on **main**.\n- one\n```bash\nnpm test --watch=false\n```" };

        var heading = (TextBlock)view.Children[0];
        var paragraph = (TextBlock)view.Children[1];
        var item = (Border)view.Children[2];
        var code = (StackPanel)view.Children[3];
        var codeText = (TextBlock)((ScrollViewer)((Border)code.Children[1]).Child!).Content!;

        Assert.That(heading.Classes, Does.Contain("au-md-heading"));
        Assert.That(paragraph.Inlines!.OfType<Run>().Select(run => run.Text), Is.EqualTo(new[] { "Run ", " npm test ", " on ", "main", "." }));
        Assert.That(paragraph.Inlines!.OfType<Run>().ElementAt(3).FontWeight, Is.EqualTo(FontWeight.SemiBold));
        Assert.That(item.Classes, Does.Contain("au-md-list-item"));
        var header = (Grid)code.Children[0];
        Assert.That(((TextBlock)header.Children[0]).Text, Is.EqualTo("bash"));
        Assert.That(((Button)header.Children[1]).Content, Is.EqualTo("Copy"));
        Assert.That(codeText.TextWrapping, Is.EqualTo(TextWrapping.NoWrap));
        Assert.That(codeText.Text, Is.EqualTo("npm test --watch=false"));
    }

    [AvaloniaTest]
    public void Markdown_view_redraws_as_a_streaming_reply_grows()
    {
        var view = new AgentMarkdownView { Markdown = "Hel" };
        view.Markdown = "Hello\n\nWorld";

        Assert.That(view.Children, Has.Count.EqualTo(2));
        view.Markdown = "";
        Assert.That(view.Children, Is.Empty);
    }

    [AvaloniaTest]
    public async Task Tool_item_keeps_fields_across_updates_and_opens_to_its_output()
    {
        var item = new AgentChatItemViewModel("Tool", "agent-up-orchestration_list_workspaces", "pending", "call-1");
        item.SetTool("agent-up-orchestration_list_workspaces", "other", null, null, "pending");
        item.SetTool(null, null, """{"workspaceId":"ws-1"}""", string.Join('\n', Enumerable.Range(1, 30).Select(index => $"line {index}")), "completed");

        Assert.That(item.Tool.Title, Is.EqualTo("List workspaces"));
        Assert.That(item.Tool.Summary, Is.EqualTo("ws-1"));
        Assert.That(item.ToolGlyph, Is.EqualTo("✓"));
        Assert.That(item.HasToolOutput, Is.False, "The row starts closed.");

        await item.ToggleToolCommand.Execute().FirstAsync();
        Assert.That(item.HasToolInput && item.HasToolOutput && item.CanShowAllOutput, Is.True);
        Assert.That(item.ShowAllOutputLabel, Is.EqualTo("Show 16 more lines"));

        await item.ShowAllOutputCommand.Execute().FirstAsync();
        Assert.That(item.ToolOutputText!.Split('\n'), Has.Length.EqualTo(30));
        Assert.That(item.CanShowAllOutput, Is.False);
    }

    [AvaloniaTest]
    public void Agent_prose_between_steps_is_not_shown_as_a_work_row()
    {
        var prose = new AgentChatItemViewModel("Agent", "Checking the workspace.");
        var plan = new AgentChatItemViewModel("Plan", "○ step");
        var failed = new AgentChatItemViewModel("Tool", "bash", "failed", "call-2");
        failed.SetTool("bash", "execute", """{"command":"npm test"}""", "Error", "failed");

        Assert.That(prose.IsAgentText && !prose.IsOtherWork && !prose.IsTool, Is.True);
        Assert.That(plan.IsOtherWork, Is.True);
        Assert.That(failed.IsToolFailed && !failed.IsToolRunning, Is.True);
        Assert.That(failed.ToolGlyph, Is.EqualTo("✕"));
    }
}
