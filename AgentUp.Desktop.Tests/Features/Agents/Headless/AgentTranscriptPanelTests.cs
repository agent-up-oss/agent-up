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
    public async Task Transcript_usesCatalogThoughtAndCardSurfaces()
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

        var transcript = app.Window.FindControl<ItemsControl>("AgentTranscript")!;
        var user = transcript.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("au-chat-user") && border.IsVisible);
        var thought = transcript.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("au-chat-thought") && button.IsVisible);
        var cards = transcript.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("au-card") && border.IsVisible).ToList();
        var body = thought.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("au-chat-thought-body"));

        Assert.Multiple(() =>
        {
            Assert.That(transcript.IsVisible, Is.True);
            Assert.That(user.IsVisible, Is.True);
            Assert.That(user.HorizontalAlignment, Is.EqualTo(Avalonia.Layout.HorizontalAlignment.Right));
            Assert.That(thought.IsVisible, Is.True);
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(body.IsVisible, Is.False);
            Assert.That(((AgentChatItemViewModel)thought.DataContext!).Label, Is.EqualTo("Thought"));
            Assert.That(transcript.GetVisualDescendants().OfType<Button>().Any(button => button.Classes.Contains("au-chat-run") && button.IsVisible), Is.False);
        });

        await app.Window.ClickControlAsync(thought);
        Assert.That(((AgentChatItemViewModel)thought.DataContext!).IsThoughtExpanded, Is.True);
        Assert.That(((AgentChatItemViewModel)thought.DataContext!).Label, Is.EqualTo("Thought"));
        Assert.That(body.IsVisible, Is.True);
        Assert.That(body.Text, Is.EqualTo("Clarifying test meaning"));
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
