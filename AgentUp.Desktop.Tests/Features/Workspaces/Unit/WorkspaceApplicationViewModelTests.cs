using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceApplicationViewModelTests
{
    [Test]
    public void UpdateState_ReturnsFalse_WhenStateAndPortHealthAreUnchanged()
    {
        var portHealth = new List<PortHealthChangeDto> { new(5001, "Healthy") };
        var vm = new WorkspaceApplicationViewModel(
            "Web",
            "web",
            "Running",
            portHealth: portHealth);

        var changed = vm.UpdateState("Running", portHealth);

        Assert.That(changed, Is.False);
    }

    [Test]
    public void UpdateState_ReturnsTrue_WhenPortHealthChanges()
    {
        var vm = new WorkspaceApplicationViewModel(
            "Web",
            "web",
            "Running",
            portHealth: [new PortHealthChangeDto(5001, "Healthy")]);

        var changed = vm.UpdateState("Running", [new PortHealthChangeDto(5001, "Unhealthy")]);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(vm.PortHealth!.Single().HealthState, Is.EqualTo("Unhealthy"));
        });
    }
}
