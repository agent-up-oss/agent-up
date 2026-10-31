using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Interface.Headless;

/// <summary>
/// Opening an agent from the command center's team hands the pane to that agent's workspace.
///
/// What shows then is the workspace detail the classic shell draws, with its own tabs and
/// everything under them, so these tests are about which shell is on screen rather than about a
/// second set of controls that would have to be kept in step with the first.
/// </summary>
[TestFixture]
public sealed class AgentWorkspacePaneTests
{
    [AvaloniaTest]
    public async Task Opening_an_agent_shows_their_workspace_detail_rather_than_the_command_center()
    {
        var driver = await LaunchOnTheCommandCenterAsync();

        await OpenAgentAsync(driver);

        Assert.Multiple(() =>
        {
            Assert.That(Detail(driver).IsVisible, Is.True, "the workspace detail is the pane");
            Assert.That(CommandCenter(driver).IsVisible, Is.False, "the command center steps aside");
            Assert.That(Rail(driver).IsVisible, Is.False, "the team picked the workspace, so the rail stands down");
        });
    }

    [AvaloniaTest]
    public async Task An_opened_agent_carries_the_workspace_tabs_the_classic_shell_carries()
    {
        var driver = await LaunchOnTheCommandCenterAsync();

        await OpenAgentAsync(driver);

        var tabs = driver.Window.FindControl<ListBox>("ShellTabs")!;

        Assert.Multiple(() =>
        {
            Assert.That(tabs.IsVisible, Is.True);
            Assert.That(
                tabs.ItemsSource!.Cast<WorkspaceShellTabItemViewModel>().Select(tab => tab.Label),
                Is.EqualTo(new[] { "Overview", "Agent", "Git" }));
            Assert.That(driver.Window.FindControl<ListBox>("ApplicationTabs")!.IsVisible, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task The_way_back_to_the_team_shows_only_while_an_agent_is_open()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        var back = BackToTeam(driver);

        Assert.That(back.IsVisible, Is.False, "nothing to go back from yet");

        await OpenAgentAsync(driver);

        Assert.That(back.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task Going_back_to_the_team_gives_the_command_center_its_pane_again()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);

        await driver.Window.ClickControlAsync(BackToTeam(driver));
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(CommandCenter(driver).IsVisible, Is.True);
            Assert.That(Rail(driver).IsVisible, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task The_classic_shell_keeps_its_rail_and_draws_no_command_center()
    {
        // The guard on all of this: with the setting off, the window is the one it has always been.
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());

        Assert.Multiple(() =>
        {
            Assert.That(Rail(driver).IsVisible, Is.True);
            Assert.That(CommandCenter(driver).IsVisible, Is.False);
            Assert.That(BackToTeam(driver).IsVisible, Is.False);
            Assert.That(Detail(driver).IsVisible, Is.True);
        });
    }

    private static async Task<AppDriver> LaunchOnTheCommandCenterAsync()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        ViewModel(driver).CommandCenter!.ToggleCommand.Execute().Subscribe();
        await HeadlessExtensions.FlushAsync();
        return driver;
    }

    private static async Task OpenAgentAsync(AppDriver driver)
    {
        var standing = TeamStandingComposition.Standing(workspaceId: DesktopDomain.WorkspaceId);
        ViewModel(driver).CommandCenter!.OpenAgentCommand.Execute(standing).Subscribe();
        await HeadlessExtensions.FlushAsync();
    }

    private static MainViewModel ViewModel(AppDriver driver) => (MainViewModel)driver.Window.DataContext!;

    private static Border Rail(AppDriver driver) => driver.Window.FindControl<Border>("SidebarRail")!;

    private static Border Detail(AppDriver driver) => driver.Window.FindControl<Border>("WorkspaceDetail")!;

    private static Control CommandCenter(AppDriver driver)
        => driver.Window.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "CommandCenterPage");

    private static Button BackToTeam(AppDriver driver)
        => driver.Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == DesktopAutomationIds.AgentBack);
}
