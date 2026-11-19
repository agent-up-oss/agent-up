using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Agents.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentTranscriptPanelTests
{
    [AvaloniaTest]
    public async Task Transcript_rightAlignsTheUsersOwnMessages()
    {
        var (_, transcript) = await OpenTranscriptWithAThoughtAsync();

        var user = transcript.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-chat-user") && border.IsVisible);

        Assert.Multiple(() =>
        {
            Assert.That(transcript.IsVisible, Is.True);
            Assert.That(user.IsVisible, Is.True);
            Assert.That(user.HorizontalAlignment, Is.EqualTo(Avalonia.Layout.HorizontalAlignment.Right));
        });
    }

    // Once the agent has spoken after its work, that work folds behind a "Worked" line and only
    // what it said stays on the canvas.
    [AvaloniaTest]
    public async Task Transcript_foldsWorkTheAgentSpokeAfterBehindAWorkedLine()
    {
        var (_, transcript) = await OpenTranscriptWithAThoughtAsync();

        var header = Header(transcript);

        Assert.Multiple(() =>
        {
            Assert.That(header.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text), Does.Contain("Worked · 1 thought"));
            Assert.That(Replies(transcript), Has.Count.EqualTo(1));
            Assert.That(VisibleThoughts(transcript), Is.Empty);
        });
    }

    [AvaloniaTest]
    public async Task Transcript_opensAThoughtToItsRawTextOnlyWhenClicked()
    {
        var (app, transcript) = await OpenTranscriptWithAThoughtAsync();
        await app.Window.ClickControlAsync(Header(transcript));
        var thought = VisibleThoughts(transcript).Single();
        var body = ThoughtBody(thought);

        Assert.That(body.IsVisible, Is.False);
        Assert.That(((AgentChatItemViewModel)thought.DataContext!).Label, Is.EqualTo("Thought"));

        await app.Window.ClickControlAsync(thought);

        Assert.Multiple(() =>
        {
            Assert.That(((AgentChatItemViewModel)thought.DataContext!).IsThoughtExpanded, Is.True);
            Assert.That(body.IsVisible, Is.True);
            Assert.That(body.Text, Is.EqualTo("Clarifying test meaning"));
        });
    }

    [AvaloniaTest]
    public async Task Transcript_followsNewOutputUntilTheReaderScrollsUp()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        var scroll = app.Window.FindControl<ScrollViewer>("AgentTranscriptScroll")!;
        foreach (var index in Enumerable.Range(0, 40))
            viewModel.Agent.Transcript.Add(new AgentChatItemViewModel("You", $"message {index}"));
        await HeadlessExtensions.FlushAsync();
        var followed = scroll.Extent.Height - (scroll.Offset.Y + scroll.Viewport.Height);

        scroll.Offset = new Avalonia.Vector(0, 0);
        await HeadlessExtensions.FlushAsync();
        viewModel.Agent.Transcript.Add(new AgentChatItemViewModel("You", "while reading"));
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(scroll.Extent.Height, Is.GreaterThan(scroll.Viewport.Height), "the transcript overflows");
            Assert.That(followed, Is.LessThanOrEqualTo(1), "new output is kept in view at the end");
            Assert.That(scroll.Offset.Y, Is.Zero, "a reader who scrolled up stays where they are");
        });
    }

    private static Button Header(ItemsControl transcript)
        => transcript.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("au-chat-run") && button.IsEffectivelyVisible);

    private static List<Button> VisibleThoughts(ItemsControl transcript)
        => transcript.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("au-chat-thought") && button.IsEffectivelyVisible)
            .ToList();

    private static TextBlock ThoughtBody(Button thought)
        => thought.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Classes.Contains("au-chat-thought-body"));

    /// <summary>A transcript holding one question and one live run that thought and replied.</summary>
    private static async Task<(AppDriver App, ItemsControl Transcript)> OpenTranscriptWithAThoughtAsync()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        viewModel.Agent.Transcript.Add(new AgentChatItemViewModel("You", "test"));
        var run = new AgentRunViewModel();
        run.Items.Add(new AgentChatItemViewModel("Thought", "**Clarifying test meaning**"));
        run.Items.Add(new AgentChatItemViewModel("Agent", "Ready", displayRole: "Codex"));
        viewModel.Agent.Transcript.Add(run);
        await HeadlessExtensions.FlushAsync();
        return (app, app.Window.FindControl<ItemsControl>("AgentTranscript")!);
    }

    [AvaloniaTest]
    public async Task Transcript_collapsesSealedRunsBehindAWorkedHeader()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        var run = new AgentRunViewModel();
        run.Items.Add(new AgentChatItemViewModel("Tool", "Search", "completed", "t1"));
        run.Items.Add(new AgentChatItemViewModel("Agent", "Done", displayRole: "Codex"));
        run.Seal();
        viewModel.Agent.Transcript.Add(run);
        viewModel.Agent.Transcript.Add(new AgentChatItemViewModel("You", "next"));
        await HeadlessExtensions.FlushAsync();

        var transcript = app.Window.FindControl<ItemsControl>("AgentTranscript")!;
        var header = transcript.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("au-chat-run") && button.IsVisible);
        Assert.That(header.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text), Does.Contain("Worked · 1 tool call"));
        Assert.That(Replies(transcript), Has.Count.EqualTo(1));
        Assert.That(ToolRows(transcript), Is.Zero, "A sealed run hides its tool calls behind the header.");

        await app.Window.ClickControlAsync(header);
        Assert.That(run.Sections.Single().IsExpanded, Is.True);
        Assert.That(Replies(transcript), Has.Count.EqualTo(1));
        Assert.That(ToolRows(transcript), Is.EqualTo(1));
    }

    // What the agent said is drawn as prose on the canvas, outside any folded section.
    private static List<AgentMarkdownView> Replies(Control transcript) => transcript.GetVisualDescendants().OfType<AgentMarkdownView>()
        .Where(view => view.Classes.Contains("au-chat-reply") && view.IsEffectivelyVisible)
        .ToList();

    private static int ToolRows(Control transcript) => transcript.GetVisualDescendants().OfType<Button>()
        .Count(button => button.Classes.Contains("au-tool-call") && button.IsVisible);
}
