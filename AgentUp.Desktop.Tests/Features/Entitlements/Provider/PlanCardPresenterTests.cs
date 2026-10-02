using AgentUp.Desktop.Features.Entitlements.DTOs;
using AgentUp.Desktop.Features.Entitlements.Providers;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Provider;

[TestFixture]
public sealed class PlanCardPresenterTests
{
    [Test]
    public void Present_rendersACommunityDocumentFromFeaturesAndLimits()
    {
        var card = PlanCardPresenter.Present(Document(
            "Community",
            "free",
            "community",
            new Dictionary<string, EntitlementFeatureDocument>
            {
                ["workspace.create"] = new(true),
                ["agent.prompt"] = new(true)
            },
            new Dictionary<string, EntitlementLimitDocument>()));

        Assert.Multiple(() =>
        {
            Assert.That(card.DisplayName, Is.EqualTo("Community"));
            Assert.That(card.Billing, Is.EqualTo("free"));
            Assert.That(card.Summary, Is.EqualTo("2 of 2 operations available"));
            Assert.That(card.Limits, Is.Empty);
        });
    }

    [Test]
    public void Present_rendersANonCommunityDocumentWithoutEditionNameBranching()
    {
        var card = PlanCardPresenter.Present(Document(
            "Team",
            "subscription",
            "team",
            new Dictionary<string, EntitlementFeatureDocument>
            {
                ["workspace.create"] = new(false),
                ["agent.prompt"] = new(true)
            },
            new Dictionary<string, EntitlementLimitDocument>
            {
                ["workspace.count"] = new(3, 2)
            }));

        Assert.Multiple(() =>
        {
            Assert.That(card.DisplayName, Is.EqualTo("Team"));
            Assert.That(card.Summary, Is.EqualTo("1 of 2 operations available"));
            Assert.That(card.Limits[0].Id, Is.EqualTo("workspace.count"));
            Assert.That(PlanCardPresenter.IsFeatureAvailable(
                Document("Team", "subscription", "team",
                    new Dictionary<string, EntitlementFeatureDocument> { ["workspace.create"] = new(false) },
                    new Dictionary<string, EntitlementLimitDocument>()),
                PlanCardPresenter.WorkspaceCreateFeature), Is.False);
        });
    }

    private static EntitlementsDocumentDto Document(
        string displayName,
        string billing,
        string edition,
        Dictionary<string, EntitlementFeatureDocument> features,
        Dictionary<string, EntitlementLimitDocument> limits)
        => new("1", "local", "admin", "selfHosted", edition, displayName, billing, "1", null, features, limits);
}
