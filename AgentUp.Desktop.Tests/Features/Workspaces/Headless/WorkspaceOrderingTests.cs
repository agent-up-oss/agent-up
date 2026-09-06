using AgentUp.Desktop.Features.Workspaces.DTOs;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class WorkspaceOrderingTests
{
    [AvaloniaTest]
    public async Task Sidebar_putsRunningWorkspacesAboveStoppedOnes()
    {
        var now = DateTimeOffset.UtcNow;
        var workspaces = new List<WorkspaceDto>
        {
            new("ws-1", "agent1", "/repo/agent1", "/worktrees/agent1", "not a git repo", "abc123", "Stopped")
            {
                LastActivityAtUtc = now
            },
            new("ws-2", "agent2", "/repo/agent2", "/worktrees/agent2", "not a git repo", "def456", "Running")
            {
                LastActivityAtUtc = now.AddMinutes(-30)
            },
        };

        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        Assert.That(app.Sidebar.WorkspaceIds, Is.EqualTo(["ws-2", "ws-1"]));
    }

    [AvaloniaTest]
    public async Task Sidebar_movesRunningWorkspaceAboveStoppedOnes_afterStartAction()
    {
        var workspaces = WorkspaceFixtures.Multiple();
        var (app, _) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        var stoppedIndex = app.Sidebar.WorkspaceIds.ToList().IndexOf("ws-2");
        await app.Sidebar.ClickStartOnWorkspaceAtIndexAsync(stoppedIndex);

        Assert.That(app.Sidebar.WorkspaceIds[0], Is.EqualTo("ws-2"));
    }
}
