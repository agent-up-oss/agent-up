using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Provider;

[TestFixture]
public sealed class DockerCapabilityPackageWriterTests
{
    [Test]
    public void Write_creates_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new DockerCapabilityPacker();
            new DockerCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void Write_creates_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new DockerCapabilityPacker();
            new DockerCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "default.nix")), Does.Contain("docker"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-docker-write-" + Guid.NewGuid().ToString("N"))).FullName;
}
