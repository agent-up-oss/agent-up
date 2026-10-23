using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class DesktopSurfaceRoutesTests
{
    [Test]
    public void All_listsTheNamedDesktopRoutes() =>
        Assert.That(
            DesktopSurfaceRoutes.All,
            Is.EqualTo((string[])
            [
                DesktopSurfaceRoutes.Login,
                DesktopSurfaceRoutes.WorkspacesList,
                DesktopSurfaceRoutes.WorkspaceOverview,
                DesktopSurfaceRoutes.WorkspaceApplications,
                DesktopSurfaceRoutes.WorkspaceGit,
                DesktopSurfaceRoutes.WorkspaceAgents
            ]));

    [TestCase(DesktopSurfaceRoutes.Login)]
    [TestCase(DesktopSurfaceRoutes.WorkspacesList)]
    [TestCase(DesktopSurfaceRoutes.WorkspaceOverview)]
    [TestCase(DesktopSurfaceRoutes.WorkspaceApplications)]
    [TestCase(DesktopSurfaceRoutes.WorkspaceGit)]
    [TestCase(DesktopSurfaceRoutes.WorkspaceAgents)]
    public void IsNamedRoute_acceptsTheCataloguedRoutes(string path) =>
        Assert.That(DesktopSurfaceRoutes.IsNamedRoute(path), Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("Login")]
    [TestCase("/login")]
    [TestCase("workspace/git/")]
    [TestCase("workspaces")]
    public void IsNamedRoute_rejectsUnknownPaths(string? path) =>
        Assert.That(DesktopSurfaceRoutes.IsNamedRoute(path), Is.False);
}
