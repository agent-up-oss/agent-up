using System.Net;
using AgentUp.Desktop.Features.Entitlements.Controllers;
using AgentUp.Desktop.Features.Entitlements.Providers;
using AgentUp.Desktop.Features.Entitlements.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Entitlements.Controller;

[TestFixture]
public sealed class EntitlementsControllerTests
{
    [Test]
    public async Task GetAsync_DelegatesToTheService()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new
        {
            displayName = "Community",
            features = new Dictionary<string, object> { ["agent.prompt"] = new { available = true } },
            limits = new Dictionary<string, object>()
        }));
        var controller = new EntitlementsController(new EntitlementsService(new EntitlementsApiClient(http.Client)));

        var document = await controller.GetAsync();

        Assert.That(document!.DisplayName, Is.EqualTo("Community"));
    }

    [Test]
    public async Task GetPlanAsync_PresentsFeaturesWithoutEditionBranching()
    {
        using var http = new DisposableTestHttpClient(_ => HttpTestResponses.Json(new
        {
            displayName = "Team",
            edition = "team",
            billing = "subscription",
            features = new Dictionary<string, object>
            {
                ["workspace.create"] = new { available = false },
                ["agent.prompt"] = new { available = true }
            },
            limits = new Dictionary<string, object>()
        }));
        var controller = new EntitlementsController(new EntitlementsService(new EntitlementsApiClient(http.Client)));

        var plan = await controller.GetPlanAsync();

        Assert.Multiple(() =>
        {
            Assert.That(plan.Card!.DisplayName, Is.EqualTo("Team"));
            Assert.That(plan.CanCreateWorkspace, Is.False);
        });
    }
}
