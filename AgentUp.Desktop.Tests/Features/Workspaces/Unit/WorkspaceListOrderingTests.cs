using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceListOrderingTests
{
    [Test]
    [TestCase("Running", 2, true)]
    [TestCase("Starting", 1, true)]
    [TestCase("Stopping", 0, false)]
    [TestCase("Failed", 0, false)]
    [TestCase("Stopped", 0, false)]
    public void ActivePriority_treatsLifecycleStatesAsExpected(string state, int priority, bool isActive)
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkspaceListOrdering.ActivePriority(state), Is.EqualTo(priority));
            Assert.That(WorkspaceListOrdering.IsActive(state), Is.EqualTo(isActive));
        });
    }
}
