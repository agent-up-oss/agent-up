using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Agents.ViewModels;
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

    // A live run has no "Worked" header of its own: its reply is shown as a card, and the
    // thought sits above it as its own collapsed surface.
    [AvaloniaTest]
    public async Task Transcript_showsALiveRunAsAThoughtAndACardWithoutAHeader()
    {
        var (_, transcript) = await OpenTranscriptWithAThoughtAsync();

        var cards = transcript.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains("au-card") && border.IsVisible)
            .ToList();
        var thought = Thought(transcript);

        Assert.Multiple(() =>
        {
            Assert.That(thought.IsVisible, Is.True);
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(
                transcript.GetVisualDescendants().OfType<Button>()
                    .Any(button => button.Classes.Contains("au-chat-run") && button.IsVisible),
                Is.False);
        });
    }

    [AvaloniaTest]
    public async Task Transcript_keepsAThoughtCollapsedUntilItIsClicked()
    {
        var (app, transcript) = await OpenTranscriptWithAThoughtAsync();
        var thought = Thought(transcript);
        var body = ThoughtBody(thought);

        Assert.That(body.IsVisible, Is.False);
        Assert.That(((AgentChatItemViewModel)thought.DataContext!).Label, Is.EqualTo("Thought"));

        await app.Window.ClickControlAsync(thought);

        Assert.Multiple(() =>
        {
            Assert.That(((AgentChatItemViewModel)thought.DataContext!).IsThoughtExpanded, Is.True);
            Assert.That(((AgentChatItemViewModel)thought.DataContext!).Label, Is.EqualTo("Thought"));
            Assert.That(body.IsVisible, Is.True);
            Assert.That(body.Text, Is.EqualTo("Clarifying test meaning"));
        });
    }

    private static Button Thought(ItemsControl transcript)
        => transcript.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("au-chat-thought") && button.IsVisible);

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
        Assert.That(header.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text), Does.Contain("Worked · 1 tool"));
        Assert.That(transcript.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("au-card") && border.IsVisible), Is.EqualTo(1));
        Assert.That(transcript.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("au-chat-work") && border.IsVisible), Is.False);

        await app.Window.ClickControlAsync(header);
        Assert.That(run.IsExpanded, Is.True);
        Assert.That(transcript.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("au-card") && border.IsVisible), Is.EqualTo(1));
        Assert.That(transcript.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("au-chat-work") && border.IsVisible), Is.EqualTo(1));
    }
}
