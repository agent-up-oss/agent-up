using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

public sealed class DesktopAutomationIdTests
{
    [AvaloniaTest]
    public async Task WorkspaceStop_isAddressableByAutomationId()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());

        var stop = ChromeTestSupport.FindDescendantByAutomationId<Button>(
            app.Window, DesktopAutomationIds.WorkspaceStop(DesktopDomain.WorkspaceId));

        Assert.That(stop, Is.Not.Null);
        Assert.That(stop!.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task ShellTabs_exposeStableAutomationIds()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());

        var overview = ChromeTestSupport.FindDescendantByAutomationId<Border>(
            app.Window, DesktopAutomationIds.ShellTabOverview);
        var agent = ChromeTestSupport.FindDescendantByAutomationId<Border>(
            app.Window, DesktopAutomationIds.ShellTabAgent);
        var git = ChromeTestSupport.FindDescendantByAutomationId<Border>(
            app.Window, DesktopAutomationIds.ShellTabGit);

        Assert.Multiple(() =>
        {
            Assert.That(overview, Is.Not.Null);
            Assert.That(agent, Is.Not.Null);
            Assert.That(git, Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task OverviewExportReport_isAddressableByAutomationId()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());

        var export = ChromeTestSupport.FindDescendantByAutomationId<Button>(
            app.Window, DesktopAutomationIds.OverviewExportReport);

        Assert.That(export, Is.Not.Null);
        Assert.That(export!.Content, Is.EqualTo("Export Report"));
        Assert.That(export.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitCommit_isAddressableByAutomationId()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        var commit = ChromeTestSupport.FindDescendantByAutomationId<Button>(
            app.Window, DesktopAutomationIds.GitCommit);

        Assert.That(commit, Is.Not.Null);
        Assert.That(commit!.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitHosting_isAddressableByAutomationId()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        var strip = ChromeTestSupport.FindDescendantByAutomationId<Border>(
            app.Window, DesktopAutomationIds.GitHosting);
        var title = ChromeTestSupport.FindDescendantByAutomationId<TextBlock>(
            app.Window, DesktopAutomationIds.GitHostingTitle);

        Assert.That(strip, Is.Not.Null);
        Assert.That(strip!.IsVisible, Is.True);
        Assert.That(title, Is.Not.Null);
        Assert.That(title!.Text, Does.Contain("forge capability"));
    }

    [AvaloniaTest]
    public async Task ValidationToggle_isAddressableByAutomationId()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([DesktopDomain.WorkspaceWithApplications().Build()]);
        await app.Content.SelectApplicationTabAsync();
        await HeadlessExtensions.FlushAsync();

        var toggle = ChromeTestSupport.FindDescendantByAutomationId<Button>(
            app.Window, DesktopAutomationIds.ValidationToggleCollapsed);

        Assert.That(toggle, Is.Not.Null);
        Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.IsVisible, Is.True);
    }
}
