using Avalonia.Headless.NUnit;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class ContentPanelBehaviorTests
{
    [AvaloniaTest]
    public async Task Content_showsEmptyState_whenNoWorkspacesRegistered()
    {
        var app = await AppDriver.LaunchEmptyAsync();

        Assert.That(app.Content.ShowsEmptyState, Is.True);
        Assert.That(app.Content.EmptyStateHint, Does.Contain("agent-up start"));
        Assert.That(app.Content.EmptyStateHint, Does.Not.Contain("register"));
        Assert.That(app.Content.ShowsWorkspaceDetail, Is.False);
        Assert.That(app.Content.ShowsError, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_showsWorkspaceDetail_whenWorkspaceIsSelected()
    {
        var workspace = DesktopDomain.Workspace().Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);

        Assert.That(app.Content.ShowsWorkspaceDetail, Is.True);
        Assert.That(app.Content.ShowsEmptyState, Is.False);
        Assert.That(app.Content.ShowsError, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_displaysCorrectWorkspaceName_inDetailPanel()
    {
        var workspace = DesktopDomain.Workspace().Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);

        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspace.DisplayName));
    }

    [AvaloniaTest]
    public async Task Content_updatesDetail_whenDifferentWorkspaceSelected()
    {
        var workspaces = DesktopDomain.Workspaces();
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        // Sidebar order after load is Running-first then alphabetical: API Gateway, My App, Auth Service.
        await app.Sidebar.SelectWorkspaceAtIndexAsync(1);

        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspaces[0].DisplayName));
    }

    [AvaloniaTest]
    public async Task Content_showsError_whenServerIsUnreachable()
    {
        var app = await AppDriver.LaunchWithServerErrorAsync();

        Assert.That(app.Content.ShowsError, Is.True);
        Assert.That(app.Content.ShowsEmptyState, Is.False);
        Assert.That(app.Content.ShowsWorkspaceDetail, Is.False);
        Assert.That(app.Content.ErrorMessage, Is.Not.Null.And.Not.Empty);
    }

    [AvaloniaTest]
    public async Task Content_autoSelectsFirstWorkspace_whenMultipleLoaded()
    {
        var workspaces = DesktopDomain.Workspaces();
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        // Sidebar order after load is Running-first then alphabetical: API Gateway, My App, Auth Service.
        Assert.That(app.Content.ShowsWorkspaceDetail, Is.True);
        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspaces[2].DisplayName));
    }

    [AvaloniaTest]
    public async Task Content_showsOverviewBranchPicker_whenWorkspaceIsSelected()
    {
        var workspace = DesktopDomain.Workspace().Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        for (var i = 0; i < 40 && viewModel.Overview.IsLoading; i++)
        {
            await Task.Delay(25);
            await HeadlessExtensions.FlushAsync();
        }

        Assert.That(app.Window.FindControl<ComboBox>("WorkspaceBranchCombo")!.IsVisible, Is.True);
        Assert.That(app.Window.FindControl<Button>("GitFetchButton")!.IsVisible, Is.True);
        Assert.That(app.Window.FindControl<Button>("GitPullButton")!.IsVisible, Is.True);
        Assert.That(app.Window.FindControl<Button>("GitPushButton")!.IsVisible, Is.True);
        Assert.That(viewModel.Git.Branch, Is.EqualTo(workspace.Branch));
    }

    [AvaloniaTest]
    public async Task Content_showsOverviewMetrics_whenWorkspaceIsSelected()
    {
        var workspace = DesktopDomain.Workspace().Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        for (var i = 0; i < 40 && viewModel.Overview.IsLoading; i++)
        {
            await Task.Delay(25);
            await HeadlessExtensions.FlushAsync();
        }

        Assert.That(app.Window.FindControl<Grid>("OverviewMetrics")!.IsVisible, Is.True);
        Assert.That(
            app.Window.FindControl<Grid>("OverviewMetrics")!
                .GetVisualDescendants()
                .OfType<Border>()
                .Count(border => border.Classes.Contains("metricsSummaryCard")),
            Is.EqualTo(6));
        Assert.That(app.Window.FindControl<Grid>("OverviewSkeleton")!.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_keepsOverviewBesideValidation_whenWorkspaceIsSelected()
    {
        var workspace = DesktopDomain.Workspace().Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        for (var i = 0; i < 40 && viewModel.Overview.IsLoading; i++)
        {
            await Task.Delay(25);
            await HeadlessExtensions.FlushAsync();
        }

        Assert.That(app.Window.FindControl<Border>("GitPanel")!.IsVisible, Is.False);
        Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.IsVisible, Is.True);
        Assert.That(app.Content.ShowsAddressNavBar, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_showsAddressNavBar_withDefaultUrl_whenHttpPortTabSelected()
    {
        var workspace = DesktopDomain.WorkspaceServing(3000).Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            workspace,
            () => throw new InvalidOperationException("no WebKit"));
        await app.Content.SelectApplicationTabAsync();

        Assert.That(app.Content.ShowsAddressNavBar, Is.True);
        Assert.That(app.Content.AddressBarText, Is.EqualTo("http://localhost:3000/"));
    }

    [AvaloniaTest]
    public async Task Content_showsNavButtons_forHttpPortTabs()
    {
        var workspace = DesktopDomain.WorkspaceServing(3000).Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            workspace,
            () => throw new InvalidOperationException("no WebKit"));
        await app.Content.SelectApplicationTabAsync();

        Assert.That(app.Content.ShowsBrowserBackButton, Is.True);
        Assert.That(app.Content.ShowsBrowserForwardButton, Is.True);
        Assert.That(app.Content.ShowsBrowserReloadButton, Is.True);
    }

    [AvaloniaTest]
    public async Task Content_blursAddressBar_whenClickingOutsideIt()
    {
        var workspace = DesktopDomain.WorkspaceServing(3000).Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            workspace,
            () => throw new InvalidOperationException("no WebKit"));
        await app.Content.SelectApplicationTabAsync();

        await app.Content.FocusAddressBarAsync();
        Assert.That(app.Content.AddressBarIsFocused, Is.True);

        await app.Content.ClickWorkspaceDetailAsync();

        Assert.That(app.Content.AddressBarIsFocused, Is.False);
    }

    [AvaloniaTest]
    public async Task ApplicationSubTabs_keepHoverOnTheRoundedBorder_notTheLabel()
    {
        var workspace = DesktopDomain.WorkspaceServing(3000).Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            workspace,
            () => throw new InvalidOperationException("no WebKit"));
        await app.Content.SelectApplicationTabAsync();

        var consoleTab = app.Window.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Classes.Contains("subTab")
                && border.Child is TextBlock label
                && label.Text == "Console");

        Assert.That(((TextBlock)consoleTab.Child!).Classes.Contains("subTab"), Is.False);
    }
}
