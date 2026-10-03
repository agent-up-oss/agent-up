using AgentUp.Desktop.Features.Entitlements.Models;
using AgentUp.Desktop.Features.Entitlements.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Unit;

[TestFixture]
public sealed class PlanCardViewModelTests
{
    [Test]
    public void Apply_rendersFeaturesLimitsAndBilling()
    {
        var vm = new PlanCardViewModel();
        vm.Apply(new PlanCard(
            "Community",
            "free",
            "2 of 2 operations available",
            true,
            [new PlanCardFeature("agent.prompt", true)],
            [new PlanCardLimit("workspace.count", 3, 1)]));

        Assert.Multiple(() =>
        {
            Assert.That(vm.DisplayName, Is.EqualTo("Community"));
            Assert.That(vm.Billing, Is.EqualTo("free"));
            Assert.That(vm.HasBilling, Is.True);
            Assert.That(vm.Summary, Is.EqualTo("2 of 2 operations available"));
            Assert.That(vm.IsVisible, Is.True);
            Assert.That(vm.Features, Has.Count.EqualTo(1));
            Assert.That(vm.Limits, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Clear_hidesTheCard()
    {
        var vm = new PlanCardViewModel();
        vm.Apply(new PlanCard("Community", "free", "ready", true, [], []));

        vm.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(vm.DisplayName, Is.Empty);
            Assert.That(vm.Billing, Is.Empty);
            Assert.That(vm.HasBilling, Is.False);
            Assert.That(vm.Summary, Is.Empty);
            Assert.That(vm.IsVisible, Is.False);
            Assert.That(vm.Features, Is.Empty);
            Assert.That(vm.Limits, Is.Empty);
        });
    }
}
