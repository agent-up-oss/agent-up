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
        viewModel.Agent.Agents.Add(DesktopDomain.AvailableAgent("Codex"));
        viewModel.Agent.Agents.Add(DesktopDomain.Agent().Named("Cursor").ExecutableMissing("agent").Build());
        await HeadlessExtensions.FlushAsync();

        var picker = app.Window.FindControl<ItemsControl>("AgentPicker")!;
        var buttons = picker.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("au-choice") && button.IsVisible)
            .ToList();
        var text = picker.GetVisualDescendants().OfType<TextBlock>()
            .Where(item => item.IsVisible)
            .Select(item => item.Text)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(picker.IsVisible, Is.True);
            Assert.That(buttons, Has.Count.EqualTo(1));
            Assert.That(buttons.Any(button => button.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("au-card"))), Is.False);
            Assert.That(buttons.Single(button => button.DataContext is AgentDescriptorDto { Agent: "Codex" }).IsEnabled, Is.True);
            Assert.That(text, Does.Contain("Executable not found"));
            Assert.That(text, Does.Contain("Install agent and keep it on PATH."));
            Assert.That(
                ChromeTestSupport.FindDescendantByAutomationId<StackPanel>(app.Window, DesktopAutomationIds.AgentPickerRow("Codex")),
                Is.Not.Null);
            Assert.That(
                ChromeTestSupport.FindDescendantByAutomationId<StackPanel>(app.Window, DesktopAutomationIds.AgentPickerRow("Cursor")),
                Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task AgentPicker_offersEnableWhenThePackageIsTheGap()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Agent;
        viewModel.Agent.Agents.Add(DesktopDomain.Agent().Named("Claude").PackageDisabled().Build());
        await HeadlessExtensions.FlushAsync();

        var picker = app.Window.FindControl<ItemsControl>("AgentPicker")!;
        var enable = picker.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Enable");
        var text = picker.GetVisualDescendants().OfType<TextBlock>().Select(item => item.Text).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(enable.IsEnabled, Is.True);
            Assert.That(enable.CommandParameter, Is.EqualTo("Claude"));
            Assert.That(text, Does.Contain("Not enabled"));
            Assert.That(text, Does.Contain("Claude is not enabled on this Server."));
            Assert.That(text, Does.Contain("Enable the Claude capability."));
            Assert.That(
                ChromeTestSupport.FindDescendantByAutomationId<StackPanel>(app.Window, DesktopAutomationIds.AgentPickerRow("Claude")),
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
