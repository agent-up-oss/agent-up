using Avalonia.Headless.NUnit;
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
        Assert.That(app.Content.ShowsWorkspaceDetail, Is.False);
        Assert.That(app.Content.ShowsError, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_showsWorkspaceDetail_whenWorkspaceIsSelected()
    {
        var workspace = WorkspaceFixtures.Single();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);

        Assert.That(app.Content.ShowsWorkspaceDetail, Is.True);
        Assert.That(app.Content.ShowsEmptyState, Is.False);
        Assert.That(app.Content.ShowsError, Is.False);
    }

    [AvaloniaTest]
    public async Task Content_displaysCorrectWorkspaceName_inDetailPanel()
    {
        var workspace = WorkspaceFixtures.Single();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace);

        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspace.DisplayName));
    }

    [AvaloniaTest]
    public async Task Content_updatesDetail_whenDifferentWorkspaceSelected()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        await app.Sidebar.SelectWorkspaceAtIndexAsync(1);

        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspaces[1].DisplayName));
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
        var workspaces = WorkspaceFixtures.Multiple();
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        Assert.That(app.Content.ShowsWorkspaceDetail, Is.True);
        Assert.That(app.Content.DisplayedWorkspaceName, Is.EqualTo(workspaces[0].DisplayName));
    }
}
