using System.Reactive.Linq;
using Avalonia.LogicalTree;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Providers;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.ViewModels;
using Avalonia.Input;
using Avalonia.Layout;
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
    public async Task The_Files_tab_hosts_the_workspace_files_and_an_open_file_is_its_own_page()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);

        await driver.Window.ClickControlAsync(ButtonNamed(driver.Window, "Files"));
        await WaitAsync(() => ViewModel(driver).Files.Nodes.Count > 0);
        var panel = driver.Window.FindControl<Border>("FilesPanel")!;
        var hosted = panel.GetVisualAncestors().Contains(CommandCenter(driver));
        await ViewModel(driver).Files.Nodes.Single(node => node.Path == DesktopDomain.FileReadme).OpenCommand.Execute();
        await WaitAsync(() => ViewModel(driver).CommandCenter!.SceneId == "agent-file");

        Assert.Multiple(() =>
        {
            Assert.That(hosted, Is.True, "the command center hosts the classic Files panel rather than a copy");
            Assert.That(ViewModel(driver).CommandCenter!.SceneId, Is.EqualTo("agent-file"));
            Assert.That(ViewModel(driver).Files.Editor.Text, Is.EqualTo("# Harbor Shop\n"));
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
    public async Task The_team_page_lays_the_rail_and_fleet_out_the_way_the_scene_specifies()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        var window = driver.Window;
        window.Width = 1440;
        window.Height = 900;
        await HeadlessExtensions.FlushAsync();

        var body = (Grid)window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-screen-body")).Child!;
        var head = (Grid)window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("aun-state-map__head")).Child!;
        var add = window.GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("au-workspace-add"));
        var mark = window.GetVisualDescendants().OfType<Image>().Single(image =>
            Math.Abs(image.Width - 18) < 0.1 && image.GetVisualAncestors().OfType<Control>().Any(parent => parent.Classes.Contains("au-logo-lockup")));
        var crossbar = window.GetVisualDescendants().OfType<Grid>()
            .Single(grid => grid.ColumnDefinitions.Count == 3 && Math.Abs(grid.Height - 2) < 0.1);
        // The bar joins the first lane's drop to the last, so its span follows the lanes drawn.
        var lanes = window.GetVisualDescendants().OfType<Border>().Count(border => border.Classes.Contains("aun-state-map__branch"));

        Assert.Multiple(() =>
        {
            Assert.That(body.ColumnDefinitions[0].Width, Is.EqualTo(new GridLength(264)));
            Assert.That(head.ColumnDefinitions, Has.Count.EqualTo(2));
            Assert.That(add.Width, Is.EqualTo(24));
            Assert.That(mark.Source, Is.Not.Null);
            Assert.That(crossbar.ColumnDefinitions[1].Width, Is.EqualTo(new GridLength(Math.Max(0, 2 * lanes - 2), GridUnitType.Star)));
        });
    }

    [AvaloniaTest]
    public async Task The_application_page_gives_the_running_app_the_remaining_pane()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);
        ViewModel(driver).CommandCenter!.ShowSection("application");
        await HeadlessExtensions.FlushAsync();

        var window = driver.Window;
        var pane = (Grid)window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-screen-pane")).Child!;
        var tab = window.GetVisualDescendants().OfType<Border>()
            .First(border => border.Classes.Contains("au-app-tab"));

        Assert.Multiple(() =>
        {
            Assert.That(ViewModel(driver).CommandCenter!.SceneId, Is.EqualTo("application"));
            Assert.That(pane.RowDefinitions[^1].Height.IsStar, Is.True);
            Assert.That(tab.Classes, Does.Contain("au-app-tab"));
        });
    }

    [AvaloniaTest]
    public async Task Catalog_tabs_keep_the_status_mark_beside_the_label()
    {
        var driver = await LaunchOnTheCommandCenterAsync(DesktopDomain.WorkspaceWithApplications());
        await OpenAgentAsync(driver);

        var first = ViewModel(driver).Applications.Applications.First().Name;
        var storefront = ButtonNamed(driver.Window, first);
        var dot = storefront.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("au-status-dot"));
        var line = (StackPanel)dot.Parent!;

        Assert.Multiple(() =>
        {
            Assert.That(line.Orientation, Is.EqualTo(Orientation.Horizontal));
            Assert.That(dot.Width, Is.EqualTo(8));
            Assert.That(dot.Height, Is.EqualTo(8));
            Assert.That(dot.CornerRadius.TopLeft, Is.EqualTo(999));
            Assert.That(dot.VerticalAlignment, Is.EqualTo(VerticalAlignment.Center));
        });
    }

    [AvaloniaTest]
    public async Task Application_chrome_uses_the_logo_the_address_field_and_a_visible_stop_glyph()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);
        ViewModel(driver).CommandCenter!.ShowSection("application");
        await HeadlessExtensions.FlushAsync();

        var page = CommandCenter(driver);
        var address = page.GetVisualDescendants().OfType<TextBox>().Single(box => box.Classes.Contains("au-address-bar"));
        var logo = page.GetVisualDescendants().OfType<Image>().Single(image => image.IsVisible && Math.Abs(image.Width - 18) < 0.1);
        var glyphs = page.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Text == "■").ToArray();
        var rail = page.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("au-validation-sidebar"));
        var flows = ViewModel(driver).Validation?.Flows.Count ?? 0;

        Assert.Multiple(() =>
        {
            Assert.That(address.Parent, Is.InstanceOf<Grid>());
            Assert.That(((Grid)address.Parent!).ColumnDefinitions[Grid.GetColumn(address)].Width.IsStar, Is.True);
            Assert.That(logo.Source, Is.Not.Null);
            Assert.That(glyphs, Has.All.Matches<TextBlock>(block => !block.Classes.Contains("au-lifecycle-button")));
            Assert.That(rail, Is.EqualTo(flows > 0));
        });
    }

    [AvaloniaTest]
    public async Task Console_subtab_selects_the_console_and_hosts_its_pane()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.WorkspaceServing().Build());
        ViewModel(driver).CommandCenter!.ToggleCommand.Execute().Subscribe();
        await HeadlessExtensions.FlushAsync();
        await OpenAgentAsync(driver);
        ViewModel(driver).CommandCenter!.ShowSection("application");
        await HeadlessExtensions.FlushAsync();

        var page = CommandCenter(driver);
        await driver.Window.ClickControlAsync(ButtonOn(page, "Console"));
        await HeadlessExtensions.FlushAsync();

        var console = ButtonOn(page, "Console");
        var host = driver.Window.FindControl<Control>("ConsoleHost")!;
        Assert.Multiple(() =>
        {
            Assert.That(ViewModel(driver).SelectedSubTab, Is.InstanceOf<ConsoleSubTabViewModel>());
            Assert.That(((Border)console.Content!).Classes, Does.Contain("au-subtab--selected"));
            Assert.That(host.GetVisualAncestors().OfType<Control>().Any(control => control.Name == "CommandCenterPage"), Is.True);
            Assert.That(page.GetVisualDescendants().OfType<TextBox>().Any(box => box.Classes.Contains("au-address-bar")), Is.False);
        });
    }

    [AvaloniaTest]
    public async Task Chat_settings_move_to_the_settings_page()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);
        ViewModel(driver).CommandCenter!.ShowSection("chat");
        await HeadlessExtensions.FlushAsync();

        var chat = driver.Window.FindControl<Control>("AgentChatPane")!;
        var settings = driver.Window.FindControl<Control>("ChatSettings")!;
        ViewModel(driver).CommandCenter!.ShowNav(2);
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(chat.GetVisualDescendants().Contains(settings), Is.False);
            Assert.That(settings.GetVisualAncestors().OfType<Control>().Any(control => control.Name == "CommandCenterPage"), Is.False);
            Assert.That(settings.Parent, Is.Null);
        });
    }

    [AvaloniaTest]
    public async Task The_validation_rail_expands_from_its_toggle()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        await OpenAgentAsync(driver);
        ViewModel(driver).CommandCenter!.ShowSection("application");
        ViewModel(driver).Validation!.Flows.Add(new ValidationFlowItemViewModel(
            new ValidationFlowDto("checkout", "ws", "shop", "Checkout", "A shopper checks out", "/", [], [], 1),
            _ => Task.CompletedTask));
        await HeadlessExtensions.FlushAsync();

        var collapsed = driver.Window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-validation-sidebar"));
        await driver.Window.ClickControlAsync(collapsed.GetVisualDescendants().OfType<Button>().Single());
        await HeadlessExtensions.FlushAsync();

        var expanded = driver.Window.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Classes.Contains("au-validation-sidebar"));
        Assert.Multiple(() =>
        {
            Assert.That(ViewModel(driver).Validation!.IsCollapsed, Is.False);
            Assert.That(expanded.Classes, Does.Not.Contain("au-validation-sidebar--collapsed"));
            Assert.That(expanded.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "Checkout"), Is.True);
        });
    }

    [AvaloniaTest]
    public async Task Tasks_opensTheDemoRepositoryAndSettingsCanOpenForgejo()
    {
        var window = await LaunchDemoCommandCenterAsync();
        var center = ((MainViewModel)window.DataContext!).CommandCenter!;
        var source = center.TaskSource!;
        await WaitAsync(() => source.IsConnected("github"));
        Assert.That(source.IsConnected("github"), Is.True, source.Error);
        center.ShowNav(1);
        await HeadlessExtensions.FlushAsync();
        await WaitAsync(() => ButtonTexts(window).Any(text => text.Contains("example/repository")));
        await window.ClickControlAsync(ButtonContaining(window, "example/repository"));
        await WaitAsync(() => ButtonTexts(window).Contains("Back"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "Dev review"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "PM review"));
        var queue = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.IsVisible && ButtonText(button) == "Queue");
        if (queue is not null)
            await window.ClickControlAsync(queue);
        await window.ClickControlAsync(VisibleButtonNamed(window, "Back"));
        center.ShowNav(2);
        await HeadlessExtensions.FlushAsync();
        await WaitAsync(() => source.IsConnected("github") && ButtonTexts(window).Contains("Forget"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "Forget"));
        await WaitAsync(() => !source.IsConnected("github"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "Forgejo"));
        await WaitAsync(() => TabSelected(window, "Forgejo"));

        Assert.Multiple(() =>
        {
            Assert.That(source.IsConnected("github"), Is.False);
            Assert.That(TabSelected(window, "Forgejo"), Is.True);
            Assert.That(center.SceneId, Is.EqualTo("settings"));
        });
        window.Close();
    }

    [AvaloniaTest]
    public async Task Settings_switchesToForgejoAndStoresThatToken()
    {
        var window = await LaunchDemoCommandCenterAsync();
        var center = ((MainViewModel)window.DataContext!).CommandCenter!;
        var source = center.TaskSource!;
        await WaitAsync(() => source.IsConnected("github"));
        center.ShowNav(2);
        await HeadlessExtensions.FlushAsync();
        await WaitAsync(() => source.IsConnected("github") && ButtonTexts(window).Contains("Forget"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "Forget"));
        await WaitAsync(() => !source.IsConnected("github"));
        await window.ClickControlAsync(VisibleButtonNamed(window, "Forgejo"));
        await WaitAsync(() => TabSelected(window, "Forgejo"));
        await HeadlessExtensions.FlushAsync();
        UrlBox(window).Text = "https://git.example.test";
        PasswordBox(window).Text = "forgejo-token";
        await window.ClickControlAsync(VisibleButtonNamed(window, "Save"));
        await WaitAsync(() => source.IsConnected("forgejo"));

        Assert.Multiple(() =>
        {
            Assert.That(source.IsConnected("github"), Is.False, source.Error);
            Assert.That(source.IsConnected("forgejo"), Is.True, source.Error);
            Assert.That(TabSelected(window, "Forgejo"), Is.True);
        });
        window.Close();
    }

    [AvaloniaTest]
    public async Task Settings_switches_back_to_the_classic_shell()
    {
        var driver = await LaunchOnTheCommandCenterAsync();
        ViewModel(driver).CommandCenter!.ShowNav(2);
        await HeadlessExtensions.FlushAsync();

        await driver.Window.ClickControlAsync(ButtonNamed(driver.Window, "Use classic"));

        Assert.Multiple(() =>
        {
            Assert.That(ViewModel(driver).CommandCenter!.ShowsClassicLayout, Is.True);
            Assert.That(CommandCenter(driver).IsVisible, Is.False);
            Assert.That(driver.Window.FindControl<Border>("WindowChrome")!.IsVisible, Is.True);
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
            Assert.That(driver.Window.FindControl<Control>("ChatSettings")!.GetLogicalAncestors().Contains(driver.Window.FindControl<Control>("WorkspaceSettingsPane")), Is.True);
            Assert.That(BackToTeam(driver).IsVisible, Is.False);
            Assert.That(Detail(driver).IsVisible, Is.True);
        });
    }

    private static Task<AppDriver> LaunchOnTheCommandCenterAsync() =>
        LaunchOnTheCommandCenterAsync(DesktopDomain.Workspace());

    private static async Task<AppDriver> LaunchOnTheCommandCenterAsync(WorkspaceDtoBuilder workspace)
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(workspace.Build());
        ViewModel(driver).CommandCenter!.ToggleCommand.Execute().Subscribe();
        await HeadlessExtensions.FlushAsync();
        return driver;
    }

    private static async Task<MainWindow> LaunchDemoCommandCenterAsync()
    {
        var backend = FakeServerTestComposition.Backend();
        var http = ServerSessionProvider.CreateClient(
            new Uri("http://127.0.0.1:5000"),
            new FakeServerMessageHandler(backend, new HttpClientHandler()));
        var connections = FakeServerTestComposition.Connections(new InMemoryServerConnectionStore(), http, backend);
        connections.Activate("fake");
        var login = new LoginViewModel(new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            connections));
        login.ShowPicker();
        login.ServerUrl = FakeServerIdentity.Url;
        await login.ConnectCommand.Execute().FirstAsync();
        var commandCenter = new CommandCenterViewModel(
            new InterfaceController(new MemoryInterfacePreferenceStore()),
            tasks: new TaskQueueViewModel(new TaskQueueController(new TaskQueueService(new TaskQueueApiClient(http)))),
            taskSource: new TaskSourceViewModel(new TaskSourceController(new TaskSourceService(new TaskSourceApiClient(http)))));
        var viewModel = MainViewModelFactory.Create(http, login, commandCenter: commandCenter);
        var window = new MainWindow(http, FakeServerTestComposition.Controller(backend)) { DataContext = viewModel };
        window.WebViewFactory = () => throw new InvalidOperationException("WebView not available in headless tests");
        window.Show();
        await viewModel.InitializeAsync();
        commandCenter.ToggleCommand.Execute().Subscribe();
        await HeadlessExtensions.FlushAsync();
        return window;
    }

    private static async Task OpenAgentAsync(AppDriver driver)
    {
        var standing = TeamStandingComposition.Standing(workspaceId: DesktopDomain.WorkspaceId);
        ViewModel(driver).CommandCenter!.OpenAgentCommand.Execute(standing).Subscribe();
        await HeadlessExtensions.FlushAsync();
    }

    private static MainViewModel ViewModel(AppDriver driver) => (MainViewModel)driver.Window.DataContext!;

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await HeadlessExtensions.FlushAsync();
            await Task.Delay(10);
        }
    }

    private static Border Rail(AppDriver driver) => driver.Window.FindControl<Border>("SidebarRail")!;

    private static Border Detail(AppDriver driver) => driver.Window.FindControl<Border>("WorkspaceDetail")!;

    private static Control CommandCenter(AppDriver driver)
        => driver.Window.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "CommandCenterPage");

    private static Button ButtonNamed(Window window, string label)
        => window.GetVisualDescendants().OfType<Button>().Single(button => ButtonText(button) == label);

    private static bool TabSelected(Window window, string label)
    {
        var button = VisibleButtonNamed(window, label);
        return button.Classes.Contains("au-app-tab--selected")
            || button.Content is Border { } face && face.Classes.Contains("au-app-tab--selected");
    }

    private static Button VisibleButtonNamed(Window window, string label)
        => window.GetVisualDescendants().OfType<Button>().First(button => button.IsEffectivelyVisible && ButtonText(button) == label);

    private static Button ButtonContaining(Window window, string text)
        => window.GetVisualDescendants().OfType<Button>().First(button => ButtonText(button).Contains(text));

    private static IReadOnlyList<string> ButtonTexts(Window window)
        => window.GetVisualDescendants().OfType<Button>().Select(ButtonText).ToArray();

    private static TextBox UrlBox(Window window)
        => window.GetVisualDescendants().OfType<TextBox>().Single(box => box.PlaceholderText == "https://git.example.com");

    private static TextBox PasswordBox(Window window)
        => window.GetVisualDescendants().OfType<TextBox>().Single(box => box.PlaceholderText == "Personal access token");

    private static Button ButtonOn(Control root, string label)
        => root.GetVisualDescendants().OfType<Button>().Single(button => ButtonText(button) == label);

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
