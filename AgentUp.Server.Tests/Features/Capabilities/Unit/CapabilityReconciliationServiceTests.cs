using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityReconciliationServiceTests
{
    [Test]
    public async Task Missing_dotnet_adapter_returns_an_unrunnable_application_with_declared_metadata()
    {
        var service = new CapabilityReconciliationService([]);
        var ports = new[] { ServerDomain.Port().Named("WEB_PORT").On(5000).Build() };
        var allocated = new[] { new PortMapping("WEB_PORT", 5000, 12000, "http") };

        var result = await service.ReconcileDotnetAsync(
            new DotnetApplicationDefinition("api", "10.0", new DotnetRunDefinition("Api.csproj"),
                Environment: new Dictionary<string, string> { ["MODE"] = "test" }), ports, allocated);

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("api"));
            Assert.That(result.Command, Is.Empty);
            Assert.That(result.CapabilityId, Is.EqualTo("dotnet"));
            Assert.That(result.CapabilityVersionRequirement, Is.EqualTo("10.0"));
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.Ports, Is.SameAs(ports));
            Assert.That(result.AllocatedPorts, Is.SameAs(allocated));
            Assert.That(result.Environment!["MODE"], Is.EqualTo("test"));
        });
    }

    [Test]
    public async Task Missing_docker_adapter_preserves_image_volume_command_and_database_settings()
    {
        var result = await new CapabilityReconciliationService([]).ReconcileDockerAsync(
            new DockerCapabilityDefinition("db", "postgres:17", Volumes: ["data:/var/lib/postgresql/data"],
                Command: ["postgres", "-c", "log_statement=all"], Database: true), [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("db"));
            Assert.That(result.Image, Is.EqualTo("postgres:17"));
            Assert.That(result.Volumes, Is.EqualTo(new[] { "data:/var/lib/postgresql/data" }));
            Assert.That(result.Args, Is.EqualTo(new[] { "postgres", "-c", "log_statement=all" }));
            Assert.That(result.Database, Is.True);
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
        });
    }
}
