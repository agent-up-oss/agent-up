using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class DesktopVisualTreeLocatorTests
{
    [AvaloniaTest]
    public void Resolve_matchesNameLabelRoleAndText()
    {
        var named = new Button { Name = "other", Content = "Save" };
        AutomationProperties.SetName(named, "save");
        var labeled = new Button { Content = "History" };
        var typed = new TextBox { Text = "feat(Git): cover" };
        var role = new CheckBox();
        var window = Show(new StackPanel { Children = { named, labeled, typed, role } });
        var locator = new DesktopVisualTreeLocator();

        Assert.Multiple(() =>
        {
            Assert.That(locator.Resolve(window, DesktopDomain.Target().Named("save").Build(), out _), Is.SameAs(named));
            Assert.That(locator.Resolve(window, DesktopDomain.Target().WithLabel("History").Build(), out _), Is.SameAs(labeled));
            Assert.That(locator.Resolve(window, DesktopDomain.Target().WithText("feat(Git)").Build(), out _), Is.SameAs(typed));
            Assert.That(locator.Resolve(window, DesktopDomain.Target().WithRole("CheckBox").Build(), out _), Is.SameAs(role));
            Assert.That(locator.Resolve(window, DesktopDomain.Target().Build(), out _), Is.Null);
        });
    }

    [AvaloniaTest]
    public void Resolve_prefersTheShownMatchWhenSeveralControlsShareAName()
    {
        var hidden = new Button { Name = "twin", Content = "Hidden", IsVisible = false };
        var shown = new Button { Name = "twin", Content = "Shown" };
        var window = Show(new StackPanel { Children = { hidden, shown } });
        var locator = new DesktopVisualTreeLocator();

        var resolved = locator.Resolve(window, DesktopDomain.Target().Named("twin").Build(), out var candidates);

        Assert.That(resolved, Is.SameAs(shown));
        Assert.That(candidates, Is.Not.Empty);
    }

    [AvaloniaTest]
    public void Resolve_prefersTheShownControlWhenSeveralShareATestId()
    {
        var hidden = new Border { IsVisible = false };
        AutomationProperties.SetAutomationId(hidden, DesktopAutomationIds.AgentPickerRow("test-agent-scripted"));
        var shown = new Button { Content = "Scripted test agent" };
        AutomationProperties.SetAutomationId(shown, DesktopAutomationIds.AgentPickerRow("test-agent-scripted"));
        var window = Show(new StackPanel { Children = { hidden, shown } });
        var locator = new DesktopVisualTreeLocator();

        var resolved = locator.Resolve(
            window,
            DesktopDomain.Target().WithTestId(DesktopAutomationIds.AgentPickerRow("test-agent-scripted")).Build(),
            out _);

        Assert.That(resolved, Is.SameAs(shown));
    }

    [AvaloniaTest]
    public void Nearby_listsControlsWhoseIdOrNameContainsTheWantedText()
    {
        var commit = new Button { Content = "Commit" };
        AutomationProperties.SetAutomationId(commit, DesktopAutomationIds.GitCommit);
        var other = new Button { Name = "git.commit.message", Content = "Message" };
        var window = Show(new StackPanel { Children = { commit, other } });
        var locator = new DesktopVisualTreeLocator();

        var nearby = locator.Nearby(window, DesktopDomain.Target().WithTestId("git.commit").Build());

        Assert.That(nearby.Select(item => item.AutomationId ?? item.Name), Does.Contain(DesktopAutomationIds.GitCommit));
        Assert.That(nearby.Select(item => item.Name), Does.Contain("git.commit.message"));
        Assert.That(
            locator.Nearby(window, DesktopDomain.Target().WithRole("Button").Build()).Select(item => item.ControlType),
            Does.Contain("Button"));
    }

    [AvaloniaTest]
    public void Describe_readsBoundsAndAccessibleTextFallbacks()
    {
        var block = new TextBlock { Text = "Hosting" };
        AutomationProperties.SetAutomationId(block, DesktopAutomationIds.GitHostingTitle);
        var unnamed = new Border { Name = "shell" };
        var window = Show(new StackPanel { Children = { block, unnamed } });
        var locator = new DesktopVisualTreeLocator();

        var described = locator.Describe(block);
        locator.Resolve(window, DesktopDomain.Target().WithTestId("missing").Build(), out var candidates);

        Assert.Multiple(() =>
        {
            Assert.That(described.AutomationId, Is.EqualTo(DesktopAutomationIds.GitHostingTitle));
            Assert.That(described.ControlType, Is.EqualTo("TextBlock"));
            Assert.That(candidates.Select(item => item.Name), Does.Contain("shell"));
        });
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 240, Content = content };
        window.Show();
        return window;
    }
}
