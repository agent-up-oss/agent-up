using AgentUp.Desktop.Features.Entitlements.Providers;
using AgentUp.Desktop.Features.Entitlements.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Unit;

[TestFixture]
public sealed class EntitlementsServiceTests
{
    [Test]
    public async Task GetPlanAsync_keepsCreateAvailableWhenTheDocumentIsMissing()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Empty(System.Net.HttpStatusCode.NotFound));
        var service = new EntitlementsService(new EntitlementsApiClient(http.Client));

        var plan = await service.GetPlanAsync();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Card, Is.Null);
            Assert.That(plan.CanCreateWorkspace, Is.True);
            Assert.That(plan.CanDeleteWorkspace, Is.True);
            Assert.That(plan.CanWriteGit, Is.True);
        });
    }

    [Test]
    public async Task GetPlanAsync_hidesCreateWhenTheFeatureIsUnavailable()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new
        {
            displayName = "Team",
            billing = "subscription",
            features = new Dictionary<string, object>
            {
                ["workspace.create"] = new { available = false },
                ["workspace.delete"] = new { available = false },
                ["git.write"] = new { available = false },
                ["agent.prompt"] = new { available = true }
            },
            limits = new Dictionary<string, object>()
        }));
        var service = new EntitlementsService(new EntitlementsApiClient(http.Client));

        var plan = await service.GetPlanAsync();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Card!.DisplayName, Is.EqualTo("Team"));
            Assert.That(plan.CanCreateWorkspace, Is.False);
            Assert.That(plan.CanDeleteWorkspace, Is.False);
            Assert.That(plan.CanWriteGit, Is.False);
            Assert.That(plan.Card.RestrictedFeatures.Select(feature => feature.Id),
                Is.EquivalentTo(new[] { "workspace.create", "workspace.delete", "git.write" }));
        });
    }
}
