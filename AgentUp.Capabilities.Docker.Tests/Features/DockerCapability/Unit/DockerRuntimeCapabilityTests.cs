using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Unit;

[TestFixture]
public sealed class DockerRuntimeCapabilityTests
{
    [Test]
    public void Host_requires_an_image()
    {
        var result = new DockerRuntimeCapability().Host(Request());

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("image"));
    }

    [Test]
    public void Host_builds_docker_run_arguments()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            [new RuntimePortMapping("DB_PORT", 5432, 15432)],
            ["data:/var/lib/postgresql/data"],
            ["postgres"]));

        Assert.That(result.CanRun, Is.True);
        Assert.That(result.FileName, Is.EqualTo("docker"));
        Assert.That(result.Arguments, Does.Contain("postgres:17"));
        Assert.That(result.Arguments, Does.Contain("15432:5432"));
    }

    private static RuntimeHostRequest Request(
        IReadOnlyDictionary<string, string>? parameters = null,
        IReadOnlyList<RuntimePortMapping>? ports = null,
        IReadOnlyList<string>? volumes = null,
        IReadOnlyList<string>? extra = null)
        => new(
            "db",
            null,
            parameters ?? new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            ports ?? [],
            volumes ?? [],
            extra ?? [],
            "workspace",
            "agentup-db");
}
