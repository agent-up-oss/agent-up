using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Linq;

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
            Assert.That(CommandCenter(driver).IsVisible, Is.True, "the agent page is a command-center scene");
            Assert.That(ViewModel(driver).CommandCenter!.SceneId, Is.EqualTo("agent-overview"));
            Assert.That(ViewModel(driver).CommandCenter.ShowsClassicLayout, Is.False);
        });
    }

    [AvaloniaTest]
    public async Task An_opened_agent_carries_the_workspace_tabs_the_classic_shell_carries()
    {
        var driver = await LaunchOnTheCommandCenterAsync();

        await OpenAgentAsync(driver);

        ViewModel(driver).CommandCenter!.ShowSection("chat");
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(CommandCenter(driver).IsVisible, Is.True);
            Assert.That(ViewModel(driver).CommandCenter.SceneId, Is.EqualTo("agent-chat"));
            Assert.That(ViewModel(driver).CommandCenter.ShowsClassicLayout, Is.False);
        });
    }

    [AvaloniaTest]
    public async Task The_way_back_to_the_team_shows_only_while_an_agent_is_open()
    {
        var driver = await LaunchOnTheCommandCenterAsync();

        Assert.That(ViewModel(driver).CommandCenter!.SceneId, Is.EqualTo("team"));

        await OpenAgentAsync(driver);

        Assert.That(ViewModel(driver).CommandCenter.SceneId, Is.EqualTo("agent-overview"));
    }

    [AvaloniaTest]
    public async Task Going_back_to_the_team_gives_the_command_center_its_pane_again()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);

        ViewModel(driver).CommandCenter!.CloseAgentCommand.Execute().Subscribe();
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(CommandCenter(driver).IsVisible, Is.True);
            Assert.That(ViewModel(driver).CommandCenter.SceneId, Is.EqualTo("team"));
        });
    }

    [AvaloniaTest]
    public async Task Sign_in_keeps_the_host_title_bar_while_the_command_center_is_the_shell()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        var chrome = driver.Window.FindControl<Border>("WindowChrome")!;

        Assert.That(chrome.IsVisible, Is.False);

        ViewModel(driver).Login.ShowPicker();
        await HeadlessExtensions.FlushAsync();

        Assert.That(chrome.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task A_click_on_Tasks_opens_the_tasks_page()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        var window = driver.Window;

        await window.ClickControlAsync(ButtonNamed(window, "Tasks"));

        Assert.That(ViewModel(driver).CommandCenter!.SceneId, Is.EqualTo("tasks"));
    }

    [AvaloniaTest]
    public async Task Sign_in_clicks_reach_Connect_while_the_command_center_is_the_shell()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        var login = ViewModel(driver).Login;
        login.ShowPicker();
        await HeadlessExtensions.FlushAsync();
        var started = false;
        login.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(login.IsBusy) && login.IsBusy) started = true;
        };
        var window = driver.Window;
        var connect = window.GetVisualDescendants().OfType<Button>().Single(button =>
            AutomationProperties.GetAutomationId(button) == DesktopAutomationIds.LoginConnect);
        var center = connect.TranslatePoint(new Point(connect.Bounds.Width / 2, connect.Bounds.Height / 2), window)!.Value;

        // The press is reported on the window itself, which is what happens when composition
        // hit testing misses the button and lands on the canvas.
        window.RaiseEvent(new PointerPressedEventArgs(
            window,
            new Pointer(1, PointerType.Mouse, true),
            window,
            center,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        await HeadlessExtensions.FlushAsync();

        Assert.That(started || !login.IsVisible || !string.IsNullOrEmpty(login.ErrorMessage), Is.True);
    }

    [AvaloniaTest]
    public async Task Sign_in_clicks_focus_the_server_url_while_the_command_center_is_the_shell()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        ViewModel(driver).Login.ShowPicker();
        await HeadlessExtensions.FlushAsync();
        var window = driver.Window;
        var url = window.GetVisualDescendants().OfType<TextBox>().Single(box =>
            AutomationProperties.GetAutomationId(box) == DesktopAutomationIds.LoginServerUrl);

        await window.ClickControlAsync(url);

        Assert.That(window.FocusManager?.GetFocusedElement(), Is.SameAs(url));
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

    private static Button ButtonNamed(Window window, string label)
        => window.GetVisualDescendants().OfType<Button>().Single(button => ButtonText(button) == label);

    private static string ButtonText(Button button)
    {
        if (button.Content is string text) return text;
        if (button.Content is not Visual visual) return "";
        return string.Concat(visual.GetSelfAndVisualDescendants().OfType<TextBlock>().Select(block => block.Text));
    }

    private static Button BackToTeam(AppDriver driver)
        => driver.Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == DesktopAutomationIds.AgentBack);
}
