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
        const string NotAGitRepository = "not a git repo";
        var now = DateTimeOffset.UtcNow;
        var workspaces = new List<WorkspaceDto>
        {
            DesktopDomain.Workspace()
                .Identified("agent1")
                .WithId(DesktopDomain.WorkspaceId)
                .OnBranch(NotAGitRepository)
                .Stopped()
                .ActiveAt(now)
                .Build(),
            DesktopDomain.Workspace()
                .Identified("agent2")
                .WithId(DesktopDomain.SecondWorkspaceId)
                .OnBranch(NotAGitRepository)
                .AtCommit(DesktopDomain.SecondCommit)
                .ActiveAt(now.AddMinutes(-30))
                .Build(),
        };

        var app = await AppDriver.LaunchWithWorkspacesAsync(workspaces);

        Assert.That(app.Sidebar.WorkspaceIds, Is.EqualTo(["ws-2", "ws-1"]));
    }

    [AvaloniaTest]
    public async Task Sidebar_movesRunningWorkspaceAboveStoppedOnes_afterStartAction()
    {
        var workspaces = DesktopDomain.Workspaces();
        var (app, _) = await AppDriver.LaunchWithMutableWorkspacesAsync(workspaces);

        var stoppedIndex = app.Sidebar.WorkspaceIds.ToList().IndexOf("ws-2");
        await app.Sidebar.ClickStartOnWorkspaceAtIndexAsync(stoppedIndex);

        Assert.That(app.Sidebar.WorkspaceIds[0], Is.EqualTo("ws-2"));
    }
}
