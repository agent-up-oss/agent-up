using AgentUp.Desktop.Features.Entitlements.Models;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Unit;

[TestFixture]
public sealed class PlanCardTests
{
    [Test]
    public void FeatureLabel_describesAvailability()
    {
        Assert.That(new PlanCardFeature("workspace.create", true).Label, Is.EqualTo("workspace.create: available"));
    }

    [Test]
    public void LimitLabel_describesUsedAndMax()
    {
        Assert.That(new PlanCardLimit("workspace.count", 3, 2).Label, Is.EqualTo("workspace.count: 2 of 3"));
    }
}
