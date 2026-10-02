using AgentUp.Desktop.Features.Workspaces.Models;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceScopeKeyTests
{
    [Test]
    public void For_distinguishesTheSameWorkspaceOnTwoServers()
    {
        Assert.That(WorkspaceScopeKey.For("server-a", "main"), Is.Not.EqualTo(WorkspaceScopeKey.For("server-b", "main")));
    }

    [Test]
    public void For_isStableForOneConnectionAndWorkspace()
    {
        Assert.That(WorkspaceScopeKey.For("server-a", "main"), Is.EqualTo("server-a:main"));
    }
}
