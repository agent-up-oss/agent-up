using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class SidebarBehaviorTests
{
    [AvaloniaTest]
    public async Task Sidebar_showsWorkspaceNames_whenExpanded()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync(WorkspaceFixtures.Multiple());

        Assert.That(app.Sidebar.IsExpanded, Is.True);
        Assert.That(app.Sidebar.IsShowingNames, Is.True);
        Assert.That(app.Sidebar.IsShowingAvatars, Is.False);
    }

    [AvaloniaTest]
    public async Task Sidebar_switchesToAvatarView_whenCollapsed()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync(WorkspaceFixtures.Multiple());

        await app.Sidebar.CollapseAsync();

        Assert.That(app.Sidebar.IsCollapsed, Is.True);
        Assert.That(app.Sidebar.IsShowingAvatars, Is.True);
        Assert.That(app.Sidebar.IsShowingNames, Is.False);
    }

    [AvaloniaTest]
    public async Task Sidebar_restoresNameView_whenExpandedAfterCollapse()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync(WorkspaceFixtures.Multiple());
        await app.Sidebar.CollapseAsync();

        await app.Sidebar.ExpandAsync();

        Assert.That(app.Sidebar.IsExpanded, Is.True);
        Assert.That(app.Sidebar.IsShowingNames, Is.True);
    }

    [AvaloniaTest]
    public async Task Sidebar_displaysAllRegisteredWorkspaces()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(workspaces.Count));
    }

    [AvaloniaTest]
    public async Task Sidebar_showsNoWorkspaces_whenNoneRegistered()
    {
        var app = await AppDriver.LaunchEmptyAsync();

        Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public async Task Sidebar_preservesSelection_acrossCollapseAndExpand()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync(WorkspaceFixtures.Multiple());
        await app.Sidebar.SelectWorkspaceAtIndexAsync(1);
        var selectedName = app.Sidebar.SelectedWorkspaceName;

        await app.Sidebar.CollapseAsync();
        await app.Sidebar.ExpandAsync();

        Assert.That(app.Sidebar.SelectedWorkspaceName, Is.EqualTo(selectedName));
    }

    [AvaloniaTest]
    public async Task Sidebar_updatesWorkspaceList_whenReloadButtonClicked()
    {
        var (app, handler) = await AppDriver.LaunchWithMutableWorkspacesAsync(WorkspaceFixtures.Multiple());
        Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(3));

        var updated = WorkspaceFixtures.Multiple();
        updated.Add(new AgentUp.Desktop.Features.Workspaces.Http.WorkspaceDto(
            "ws-4", "New Service", "/repo/new", "/worktrees/new", "main", "aaa000", "Stopped"));
        handler.SetWorkspaces(updated);

        await app.Sidebar.ClickReloadAsync();

        Assert.That(app.Sidebar.WorkspaceCount, Is.EqualTo(4));
    }
}
