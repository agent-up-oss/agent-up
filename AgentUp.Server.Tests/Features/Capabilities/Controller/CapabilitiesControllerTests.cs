using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;

namespace AgentUp.Server.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilitiesControllerTests
{
    [Test]
    public async Task ReconcileDotnetAsync_returns_the_service_application_contract()
    {
        var controller = new CapabilitiesController(new CapabilityReconciliationService([]));

        var result = await controller.ReconcileDotnetAsync(
            new DotnetApplicationDefinition("web", null, new DotnetRunDefinition("Web.csproj")), [], []);

        Assert.That(result.CapabilityId, Is.EqualTo("dotnet"));
        Assert.That(result.CapabilityStatus!.Messages.Single(), Does.Contain("not installed"));
    }

    [Test]
    public async Task ReconcileDockerAsync_returns_the_service_application_contract()
    {
        var controller = new CapabilitiesController(new CapabilityReconciliationService([]));

        var result = await controller.ReconcileDockerAsync(
            new DockerCapabilityDefinition("cache", "redis:7"), [], []);

        Assert.That(result.CapabilityId, Is.EqualTo("docker"));
        Assert.That(result.Image, Is.EqualTo("redis:7"));
    }
}
