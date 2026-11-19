using System.Text.Json;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Agents.Views;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

/// <summary>
/// Replays the event stream a real OpenCode session against the CPU model produced, seven turns of
/// write, read, edit, run, search, an Agent-Up MCP call and a markdown answer, and holds the
/// transcript to what a reader should see. The Chat module replays the same file.
/// </summary>
[TestFixture]
public sealed class AgentGoldenTranscriptTests
{
    [AvaloniaTest]
    public async Task Every_tool_of_the_real_session_reads_as_its_action()
    {
        var view = await ReplayAsync();

        var tools = Runs(view).SelectMany(run => run.WorkItems).Where(item => item.IsTool).ToArray();

        Assert.That(tools.Select(item => item.Tool.Title), Is.EqualTo(new[] { "Write", "Read", "Edit", "Run", "Search", "List workspaces" }));
        Assert.That(tools.Select(item => item.Tool.State), Is.All.EqualTo("done"));
        Assert.That(tools[^1].Tool.Source, Is.EqualTo("Agent-Up"));
        Assert.That(tools[3].Tool.Summary, Is.EqualTo("ls -la"));
        Assert.That(tools[1].Tool.FullOutput, Does.StartWith("(Showing lines 1-0 of 1"), "A read shows the file text OpenCode returned, without its tags.");
        Assert.That(tools[4].Tool.Summary, Is.EqualTo("*.md"), "A search names its pattern, not the directory '.'.");
    }

    [AvaloniaTest]
    public async Task The_write_and_the_edit_are_shown_as_diffs()
    {
        var view = await ReplayAsync();
        var tools = Runs(view).SelectMany(run => run.WorkItems).Where(item => item.IsTool).ToArray();
        var write = tools[0];
        var edit = tools[2];

        Assert.That(write.HasToolDiff && edit.HasToolDiff, Is.True);
        Assert.That(write.HasDiffRemoved, Is.False, "A new file removed nothing.");
        Assert.That(write.Tool.Diff!.Added, Is.GreaterThanOrEqualTo(3), "The table header, rule and rows were added.");
        Assert.That(edit.DiffAdded, Is.EqualTo("+1"));
        Assert.That(edit.DiffRemoved, Is.EqualTo("−1"));
        Assert.That(edit.Tool.Diff!.Lines.Single(line => line.Kind == "added").Text, Does.Contain("3001"));
        Assert.That(edit.Tool.Diff.Lines.Single(line => line.Kind == "deleted").Text, Does.Contain("3000"));
        Assert.That(edit.HasToolOutput, Is.False, "An edit shows its diff, not the tool's report text.");
    }

    [AvaloniaTest]
    public async Task The_markdown_answer_draws_its_heading_and_table_rather_than_a_code_block()
    {
        var view = await ReplayAsync();
        var reply = Runs(view).Last().Reply!;

        var markdown = new AgentMarkdownView { Markdown = reply.Text };

        Assert.That(markdown.Children.OfType<Border>().Any(child => child.Classes.Contains("au-md-table")), Is.True);
        Assert.That(markdown.Children.OfType<TextBlock>().Any(child => child.Classes.Contains("au-md-heading")), Is.True);
        Assert.That(markdown.Children.OfType<Border>().Where(child => child.Classes.Contains("au-md-code-block")), Is.Empty, "The model's ```markdown wrapper is not drawn as code.");
    }

    [AvaloniaTest]
    public async Task Each_turn_reports_how_long_it_worked()
    {
        var view = await ReplayAsync();

        var summaries = Runs(view).SelectMany(run => run.Sections).Select(section => section.Summary).ToArray();

        // Each stretch of work runs from the prompt to its last step, so the time the model then
        // spends writing its answer is not counted as work.
        Assert.That(summaries, Is.EqualTo(new[]
        {
            "Worked for 48s · 1 edit", "Worked for 12s · 1 read", "Worked for 17s · 1 edit",
            "Worked for 11s · 1 tool call", "Worked for 12s · 1 search", "Worked for 14s · 1 tool call"
        }));
    }

    private static IEnumerable<AgentRunViewModel> Runs(AgentChatViewModel view) => view.Transcript.OfType<AgentRunViewModel>();

    private static async Task<AgentChatViewModel> ReplayAsync()
    {
        var hang = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = File.ReadLines(Path.Join(TestContext.CurrentContext.TestDirectory, "Fixtures", "opencode-cpu-session.jsonl"))
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                return new AgentEventDto(root.GetProperty("sequence").GetInt64(), root.GetProperty("type").GetString()!, root.GetProperty("payload").Clone(), root.GetProperty("timestamp").GetDateTimeOffset());
            })
            .ToList();
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(new StreamApiFake { EventsHang = hang, Events = events })));
        await view.LoadAsync("ws-1");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (view.Messages.Count(item => item.Role == "You") < 7 || !Runs(view).Any() || !Runs(view).Last().HasReply)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(20, timeout.Token);
        }

        hang.TrySetResult();
        return view;
    }
}
