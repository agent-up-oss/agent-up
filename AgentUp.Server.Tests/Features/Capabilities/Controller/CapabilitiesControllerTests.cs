using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilitiesControllerTests
{
    [Test]
    public async Task ReconcileDotnetAsync_returns_the_service_application_contract()
    {
        var controller = new CapabilitiesController(new CapabilityReconciliationService());

        var result = await controller.ReconcileDotnetAsync(
            new DotnetApplicationDefinition("web", null, new DotnetRunDefinition("Web.csproj")), [], []);

        Assert.That(result.CapabilityId, Is.EqualTo("dotnet"));
        Assert.That(result.CapabilityStatus!.Messages, Has.Some.Contains("not enabled"));
        Assert.That(result.CapabilityStatus.Messages, Has.Some.Contains("capabilities bootstrap"));
    }

    [Test]
    public async Task ReconcileDockerAsync_returns_the_service_application_contract()
    {
        var controller = new CapabilitiesController(new CapabilityReconciliationService());

        var result = await controller.ReconcileDockerAsync(
            new DockerCapabilityDefinition("cache", "redis:7"), [], []);

        Assert.That(result.CapabilityId, Is.EqualTo("docker"));
        Assert.That(result.Image, Is.EqualTo("redis:7"));
    }

    [Test]
    public async Task ReconcileDotnetAsync_names_nix_when_it_is_missing()
    {
        var controller = new CapabilitiesController(new CapabilityReconciliationService(
            nix: new CapabilityModuleHarness.FixedNixPresence(false)));

        var result = await controller.ReconcileDotnetAsync(
            new DotnetApplicationDefinition("web", null, new DotnetRunDefinition("Web.csproj")), [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.CapabilityStatus.Messages, Has.Some.Contains("Nix"));
            Assert.That(result.CapabilityStatus.Messages, Has.Some.Contains("https://nixos.org/download"));
            Assert.That(result.CapabilityStatus.Messages, Has.None.Contains("not enabled"));
        });
    }
}
