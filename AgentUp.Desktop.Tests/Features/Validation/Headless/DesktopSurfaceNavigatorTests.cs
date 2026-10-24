using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class DesktopSurfaceNavigatorTests
{
    [AvaloniaTest]
    public async Task Navigate_rejectsABlankOrUnknownRoute()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var navigator = new DesktopSurfaceNavigator();
        var viewModel = (MainViewModel)app.Window.DataContext!;

        Assert.That(navigator.Navigate(viewModel, null), Does.Contain("named surface route"));
        Assert.That(navigator.Navigate(viewModel, "/settings"), Does.Contain("named surface route"));
    }

    [AvaloniaTest]
    public async Task Navigate_opensLoginAndBlocksWorkspaceRoutesUntilDismissed()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var navigator = new DesktopSurfaceNavigator();
        var viewModel = (MainViewModel)app.Window.DataContext!;

        Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.Login), Is.Null);
        Assert.That(viewModel.Login.IsVisible, Is.True);
        Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspacesList), Does.Contain("login is still visible"));
        Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspaceOverview), Does.Contain("login is still visible"));

        viewModel.Login.Dismiss();
        Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspacesList), Is.Null);
    }

    [AvaloniaTest]
    public async Task Navigate_selectsEachWorkspaceShellTab()
    {
        var app = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var navigator = new DesktopSurfaceNavigator();
        var viewModel = (MainViewModel)app.Window.DataContext!;

        Assert.Multiple(() =>
        {
            Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspaceOverview), Is.Null);
            Assert.That(viewModel.SelectedShellTab, Is.EqualTo(WorkspaceShellTab.Overview));
            Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspaceApplications), Is.Null);
            Assert.That(viewModel.SelectedShellTab, Is.EqualTo(WorkspaceShellTab.Application));
            Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspaceAgents), Is.Null);
            Assert.That(viewModel.SelectedShellTab, Is.EqualTo(WorkspaceShellTab.Agent));
            Assert.That(navigator.Navigate(viewModel, DesktopNamedRoutes.WorkspaceGit), Is.Null);
            Assert.That(viewModel.SelectedShellTab, Is.EqualTo(WorkspaceShellTab.Git));
        });
    }
}
