using AgentUp.Capabilities.Docker.Features.DockerCapability.Controllers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Controller;

[TestFixture]
public sealed class DockerCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            new DockerCapabilityPackerController(
                new DockerCapabilityPackerService(new DockerCapabilityPacker(), new DockerCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void Pack_writes_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            new DockerCapabilityPackerController(
                new DockerCapabilityPackerService(new DockerCapabilityPacker(), new DockerCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "default.nix")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-docker-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}
