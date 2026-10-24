using AgentUp.Capabilities.Forgejo.Composition;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Controllers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

namespace AgentUp.Capabilities.Forgejo.Tests.Features.ForgejoCapability.Controller;

[TestFixture]
public sealed class ForgejoCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_a_forge_kind_manifest()
    {
        var directory = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-forgejo-pack-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            new ForgejoCapabilityPackerController(
                new ForgejoCapabilityPackerService(new ForgejoCapabilityPacker(), new ForgejoCapabilityPackageWriter()))
                .Pack(directory);

            var manifest = File.ReadAllText(Path.Join(directory, "capability.json"));
            Assert.That(manifest, Does.Contain("\"kind\": \"forge\""));
            Assert.That(manifest, Does.Contain("\"id\": \"forgejo\""));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void PackerHost_packs_into_the_given_directory()
    {
        var directory = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-forgejo-host-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.That(ForgejoPackerHost.Pack(directory), Is.EqualTo(directory));
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
