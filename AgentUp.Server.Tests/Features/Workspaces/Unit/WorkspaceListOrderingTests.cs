using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceListOrderingTests
{
    [TestCase(WorkspaceState.Running, true, 2)]
    [TestCase(WorkspaceState.Degraded, true, 2)]
    [TestCase(WorkspaceState.Starting, true, 1)]
    [TestCase(WorkspaceState.Failed, false, 0)]
    [TestCase(WorkspaceState.Stopped, false, 0)]
    public void ActiveWorkspaces_includeDegradedBesideRunning(
        WorkspaceState state,
        bool active,
        int priority)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkspaceListOrdering.IsActive(state), Is.EqualTo(active));
            Assert.That(WorkspaceListOrdering.ActivePriority(state), Is.EqualTo(priority));
        });
    }
}
