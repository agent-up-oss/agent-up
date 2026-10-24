using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class MobileSurfaceRoutesTests
{
    [Test]
    public void All_listsTheExpoRouterRoutes() =>
        Assert.That(
            MobileSurfaceRoutes.All,
            Is.EqualTo((string[])
            [
                MobileSurfaceRoutes.Connect,
                MobileSurfaceRoutes.Workspaces,
                MobileSurfaceRoutes.Workspace,
                MobileSurfaceRoutes.WorkspaceGit,
                MobileSurfaceRoutes.WorkspaceGitReview,
                MobileSurfaceRoutes.WorkspaceGitHistory,
                MobileSurfaceRoutes.WorkspaceAgents,
                MobileSurfaceRoutes.WorkspaceAgent,
                MobileSurfaceRoutes.WorkspaceSettings,
                MobileSurfaceRoutes.WorkspaceApplication
            ]));

    [TestCase(MobileSurfaceRoutes.Connect)]
    [TestCase(MobileSurfaceRoutes.Workspaces)]
    [TestCase(MobileSurfaceRoutes.Workspace)]
    [TestCase(MobileSurfaceRoutes.WorkspaceGit)]
    [TestCase(MobileSurfaceRoutes.WorkspaceGitReview)]
    [TestCase(MobileSurfaceRoutes.WorkspaceGitHistory)]
    [TestCase(MobileSurfaceRoutes.WorkspaceAgents)]
    [TestCase(MobileSurfaceRoutes.WorkspaceAgent)]
    [TestCase(MobileSurfaceRoutes.WorkspaceSettings)]
    [TestCase(MobileSurfaceRoutes.WorkspaceApplication)]
    [TestCase("/workspace/abc123")]
    [TestCase("/workspace/abc123/git")]
    [TestCase("/workspace/abc123/git/review")]
    [TestCase("/workspace/abc123/git/history")]
    [TestCase("/workspace/abc123/agents")]
    [TestCase("/workspace/abc123/agent")]
    [TestCase("/workspace/abc123/settings")]
    [TestCase("/workspace/abc123/application/Docs")]
    public void IsExpoRoute_acceptsCataloguedAndResolvedPaths(string path) =>
        Assert.That(MobileSurfaceRoutes.IsExpoRoute(path), Is.True);

    [Test]
    public void WorkspaceApplications_aliasesTheWorkspaceRoute() =>
        Assert.That(MobileSurfaceRoutes.WorkspaceApplications, Is.EqualTo(MobileSurfaceRoutes.Workspace));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("connect")]
    [TestCase("/connect/")]
    [TestCase("/git")]
    [TestCase("workspace/git")]
    [TestCase("/workspace/{workspaceId}/")]
    [TestCase("/workspace/abc/git/extra")]
    [TestCase("/workspace\\git")]
    [TestCase("\\connect")]
    public void IsExpoRoute_rejectsUnknownPaths(string? path) =>
        Assert.That(MobileSurfaceRoutes.IsExpoRoute(path), Is.False);
}
