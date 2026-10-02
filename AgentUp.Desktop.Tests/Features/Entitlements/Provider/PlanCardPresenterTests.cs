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

    [Test]
    public void Present_returnsUnavailableWhenTheDocumentIsMissing()
    {
        var card = PlanCardPresenter.Present(null);

        Assert.Multiple(() =>
        {
            Assert.That(card.DisplayName, Is.EqualTo("Plan unavailable"));
            Assert.That(card.Billing, Is.Empty);
            Assert.That(card.Available, Is.False);
            Assert.That(card.Features, Is.Empty);
            Assert.That(card.Limits, Is.Empty);
            Assert.That(PlanCardPresenter.IsFeatureAvailable(null, PlanCardPresenter.WorkspaceCreateFeature), Is.False);
        });
    }

    [Test]
    public void Present_defaultsWhenFeaturesLimitsAndNamesAreMissing()
    {
        var document = new EntitlementsDocumentDto(
            "1", "local", "admin", "selfHosted", "community", null, null, "rev", "2030-01-01", null, null);

        var card = PlanCardPresenter.Present(document);

        Assert.Multiple(() =>
        {
            Assert.That(card.DisplayName, Is.EqualTo("Plan"));
            Assert.That(card.Billing, Is.Empty);
            Assert.That(card.Summary, Is.EqualTo("0 of 0 operations available"));
            Assert.That(card.Available, Is.True);
            Assert.That(document.ApiVersion, Is.EqualTo("1"));
            Assert.That(document.ConnectionId, Is.EqualTo("local"));
            Assert.That(document.Subject, Is.EqualTo("admin"));
            Assert.That(document.Source, Is.EqualTo("selfHosted"));
            Assert.That(document.Edition, Is.EqualTo("community"));
        });
    }

    [Test]
    public void IsFeatureAvailable_isTrueOnlyWhenTheNamedFeatureIsEnabled()
    {
        var document = Document(
            "Community",
            "free",
            "community",
            new Dictionary<string, EntitlementFeatureDocument> { ["agent.prompt"] = new(true) },
            new Dictionary<string, EntitlementLimitDocument> { ["workspace.count"] = new(null, 1) });

        Assert.Multiple(() =>
        {
            Assert.That(PlanCardPresenter.IsFeatureAvailable(document, "agent.prompt"), Is.True);
            Assert.That(PlanCardPresenter.IsFeatureAvailable(document, PlanCardPresenter.WorkspaceCreateFeature), Is.False);
            Assert.That(document.Revision, Is.EqualTo("1"));
            Assert.That(document.ExpiresAt, Is.Null);
            Assert.That(document.Limits!["workspace.count"].Max, Is.Null);
            Assert.That(document.Limits["workspace.count"].Used, Is.EqualTo(1));
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
