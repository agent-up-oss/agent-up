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

    [Test]
    public void RestrictedFeatures_listsOnlyUnavailableOperations()
    {
        var card = new PlanCard(
            "Team",
            "subscription",
            "1 of 2 operations available",
            true,
            [new PlanCardFeature("agent.prompt", true), new PlanCardFeature("git.write", false)],
            []);

        Assert.Multiple(() =>
        {
            Assert.That(card.HasRestrictions, Is.True);
            Assert.That(card.RestrictedFeatures.Select(feature => feature.Id), Is.EqualTo(new[] { "git.write" }));
        });
    }
}
