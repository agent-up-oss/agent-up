using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentPickerPanelTests
{
    [AvaloniaTest]
    public async Task AgentPicker_usesCatalogChoiceButtonsInsteadOfWrappedCards()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        viewModel.Agent.Agents.Add(new AgentDescriptorDto("Codex", true, "Codex"));
        viewModel.Agent.Agents.Add(new AgentDescriptorDto("Cursor", false, "Cursor"));
        await HeadlessExtensions.FlushAsync();

        var picker = app.Window.FindControl<ItemsControl>("AgentPicker")!;
        var buttons = picker.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("au-choice")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(picker.IsVisible, Is.True);
            Assert.That(buttons, Has.Count.EqualTo(2));
            Assert.That(buttons.Any(button => button.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("au-card"))), Is.False);
            Assert.That(buttons.Single(button => button.DataContext is AgentDescriptorDto { Agent: "Codex" }).IsEnabled, Is.True);
            Assert.That(buttons.Single(button => button.DataContext is AgentDescriptorDto { Agent: "Cursor" }).IsEnabled, Is.False);
            Assert.That(
                ChromeTestSupport.FindDescendantByAutomationId<Button>(app.Window, DesktopAutomationIds.AgentPickerRow("Codex")),
                Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task AgentSessionList_rendersAgentDescriptionAndBranchInOneChoice()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        var session = new AgentSessionSummaryDto("saved", "Claude", "Fix session persistence", "feature/sessions", DateTimeOffset.UtcNow);
        viewModel.Agent.Sessions.Add(new AgentSessionRowViewModel(session, false, "idle"));
        await HeadlessExtensions.FlushAsync();

        var list = app.Window.FindControl<ItemsControl>("AgentSessionList")!;
        var button = list.GetVisualDescendants().OfType<Button>().Single(item => item.DataContext is AgentSessionRowViewModel { SessionId: "saved" });
        var text = button.GetVisualDescendants().OfType<TextBlock>().Select(item => item.Text).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(button.Classes, Does.Contain("au-choice"));
            Assert.That(text, Does.Contain("Claude"));
            Assert.That(text, Does.Contain("Fix session persistence"));
            Assert.That(text, Does.Contain("feature/sessions"));
            Assert.That(text, Does.Contain("Resume session →"));
            Assert.That(
                ChromeTestSupport.FindDescendantByAutomationId<Button>(
                    app.Window, DesktopAutomationIds.AgentSession("saved")),
                Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task AgentSessionList_labelsTheLiveSessionAsCurrent()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        var current = new AgentSessionSummaryDto("live", "Codex", "Restock featured products", "main", DateTimeOffset.UtcNow);
        viewModel.Agent.Sessions.Add(new AgentSessionRowViewModel(current, true, "ready"));
        await HeadlessExtensions.FlushAsync();

        var list = app.Window.FindControl<ItemsControl>("AgentSessionList")!;
        var button = list.GetVisualDescendants().OfType<Button>().Single(item => item.DataContext is AgentSessionRowViewModel { SessionId: "live" });
        var text = button.GetVisualDescendants().OfType<TextBlock>().Select(item => item.Text).ToArray();

        Assert.That(text, Does.Contain("Current · ready"));
    }

    [AvaloniaTest]
    public async Task AgentSessionList_showsEmptyCopyWhenNothingIsSaved()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        viewModel.Agent.Sessions.Clear();
        await HeadlessExtensions.FlushAsync();

        Assert.That(viewModel.Agent.HasNoSavedSessions, Is.True);
        var copy = app.Window.GetVisualDescendants().OfType<TextBlock>()
            .Single(item => item.Text == "No saved sessions yet.");
        Assert.That(copy.IsVisible, Is.True);
    }
}
