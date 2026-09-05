using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class WorkspaceLifecycleTests
{
    [AvaloniaTest]
    public async Task StartButton_startsStoppedWorkspace()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickStartOnWorkspaceAtIndexAsync(1);

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestPaths, Does.Contain("/api/workspaces/ws-2/start"));
            Assert.That(app.Sidebar.WorkspaceStateAtIndex(1), Is.EqualTo("Running"));
        });
    }

    [AvaloniaTest]
    public async Task StopButton_stopsRunningWorkspace()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickStopOnWorkspaceAtIndexAsync(0);

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestPaths, Does.Contain("/api/workspaces/ws-1/stop"));
            Assert.That(app.Sidebar.WorkspaceStateAtIndex(0), Is.EqualTo("Stopped"));
        });
    }

    [AvaloniaTest]
    public async Task DeleteButton_showsConfirmationOverlay_andRemovesWorkspaceAfterConfirm()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        await app.Sidebar.ClickDeleteOnWorkspaceAtIndexAsync(1);

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
        var workspaces = WorkspaceFixtures.Multiple();
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
