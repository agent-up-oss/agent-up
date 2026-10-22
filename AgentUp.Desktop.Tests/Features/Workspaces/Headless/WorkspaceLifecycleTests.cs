using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class WorkspaceLifecycleTests
{
    // DesktopDomain.Workspaces() sorts to [API Gateway (Running), My App (Running), Auth Service (Stopped)]
    // once loaded: active workspaces first, ties broken alphabetically.

    [AvaloniaTest]
    public async Task Launch_loadsTheProposalQueueWithoutFailingTheWorkspace()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync(DesktopDomain.Workspaces());
        var viewModel = (MainViewModel)app.Window.DataContext!;

        Assert.That(() => viewModel.Git.IsLoading, Is.False.After(1000).PollEvery(20));
        Assert.That(viewModel.Git.ErrorMessage, Is.Null);
        Assert.That(viewModel.Git.HasQueuedProposals, Is.False);
    }

    [AvaloniaTest]
    public async Task StartButton_startsStoppedWorkspace()
    {
        var workspaces = DesktopDomain.Workspaces();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickStartOnWorkspaceAtIndexAsync(2);

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestPaths, Does.Contain("/api/workspaces/ws-2/start"));
            Assert.That(app.Sidebar.WorkspaceStateById("ws-2"), Is.EqualTo("Running"));
        });
    }

    [AvaloniaTest]
    public async Task DegradedWorkspace_showsStopAndKeepsTheWarningLed()
    {
        var workspaces = new List<WorkspaceDto>
        {
            DesktopDomain.Workspace()
                .InState("Degraded")
                .WithApplication(DesktopDomain.Application().InState("Running").Build())
                .WithApplication(DesktopDomain.DocsApplication().InState("Failed").Build())
                .Build(),
            DesktopDomain.SecondWorkspace().InState("Failed").Build()
        };
        var (app, _) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);
        var degraded = app.Sidebar.WorkspaceIds
            .Select(id => ((MainViewModel)app.Window.DataContext!).Sidebar.Workspaces.First(item => item.Id == id))
            .Single(item => item.State == "Degraded");

        Assert.Multiple(() =>
        {
            Assert.That(app.Sidebar.WorkspaceStateById(degraded.Id), Is.EqualTo("Degraded"));
            Assert.That(degraded.ShowStopButton, Is.True);
            Assert.That(degraded.ShowStartButton, Is.False);
            Assert.That(degraded.StateColor, Is.EqualTo("#e0a128"));
        });

        await app.Sidebar.ClickStopOnWorkspaceAtIndexAsync(app.Sidebar.WorkspaceIds.ToList().IndexOf(degraded.Id));
        Assert.That(app.Sidebar.WorkspaceStateById(degraded.Id), Is.EqualTo("Stopped"));
    }

    [AvaloniaTest]
    public async Task FailedWorkspace_showsStartAndKeepsTheDangerLed()
    {
        var workspaces = new List<WorkspaceDto>
        {
            DesktopDomain.Workspace().InState("Failed").Build(),
            DesktopDomain.SecondWorkspace().InState("Stopped").Build()
        };
        var (app, _) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);
        var failed = ((MainViewModel)app.Window.DataContext!).Sidebar.Workspaces
            .Single(item => item.State == "Failed");

        Assert.Multiple(() =>
        {
            Assert.That(app.Sidebar.WorkspaceStateById(failed.Id), Is.EqualTo("Failed"));
            Assert.That(failed.ShowStartButton, Is.True);
            Assert.That(failed.ShowStopButton, Is.False);
            Assert.That(failed.StateColor, Is.EqualTo("#d84f4f"));
        });

        await app.Sidebar.ClickStartOnWorkspaceAtIndexAsync(app.Sidebar.WorkspaceIds.ToList().IndexOf(failed.Id));
        Assert.That(app.Sidebar.WorkspaceStateById(failed.Id), Is.EqualTo("Running"));
    }

    [AvaloniaTest]
    public async Task StopButton_stopsRunningWorkspace()
    {
        var workspaces = DesktopDomain.Workspaces();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickStopOnWorkspaceAtIndexAsync(1);

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestPaths, Does.Contain("/api/workspaces/ws-1/stop"));
            Assert.That(app.Sidebar.WorkspaceStateById("ws-1"), Is.EqualTo("Stopped"));
        });
    }

    [AvaloniaTest]
    public async Task DeleteButton_showsConfirmationOverlay_andRemovesWorkspaceAfterConfirm()
    {
        var workspaces = DesktopDomain.Workspaces();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickDeleteOnWorkspaceAtIndexAsync(2);

        Assert.Multiple(() =>
        {
            Assert.That(app.Sidebar.ShowsDeleteConfirmation, Is.True);
            Assert.That(app.Sidebar.DeleteOverlayCoversContentOnly, Is.True);
        });

        await app.Sidebar.ConfirmDeleteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestPaths, Does.Contain("/api/workspaces/ws-2"));
            Assert.That(app.Sidebar.ShowsDeleteConfirmation, Is.False);
            Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(2));
        });
    }

    [AvaloniaTest]
    public async Task DeleteConfirmation_cancel_hidesOverlayWithoutRemovingWorkspace()
    {
        var workspaces = DesktopDomain.Workspaces();
        var (app, _) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickDeleteOnWorkspaceAtIndexAsync(1);
        await app.Sidebar.CancelDeleteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(app.Sidebar.ShowsDeleteConfirmation, Is.False);
            Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(3));
        });
    }
}
