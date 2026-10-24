using AgentUp.Desktop.Features.Validation.Models;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

public sealed class DesktopNamedRoutesTests
{
    [Test]
    public void IsNamedRoute_acceptsTheDesktopSurfaceRoutes()
    {
        Assert.That(DesktopNamedRoutes.All.All(DesktopNamedRoutes.IsNamedRoute), Is.True);
        Assert.That(DesktopNamedRoutes.IsNamedRoute("workspace/git"), Is.True);
    }

    [Test]
    public void IsNamedRoute_rejectsApplicationPaths()
    {
        Assert.That(DesktopNamedRoutes.IsNamedRoute("/settings"), Is.False);
        Assert.That(DesktopNamedRoutes.IsNamedRoute(null), Is.False);
    }
}
