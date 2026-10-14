using AgentUp.Capabilities.Github.Composition;
using AgentUp.Capabilities.Github.Features.GithubCapability.Controllers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Services;

namespace AgentUp.Capabilities.Github.Tests.Features.GithubCapability.Controller;

[TestFixture]
public sealed class GithubCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_a_forge_kind_manifest()
    {
        var directory = Directory.CreateDirectory(
            Path.Join(Path.GetTempPath(), "agent-up-github-pack-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            new GithubCapabilityPackerController(
                new GithubCapabilityPackerService(new GithubCapabilityPacker(), new GithubCapabilityPackageWriter()))
                .Pack(directory);

            var manifest = File.ReadAllText(Path.Join(directory, "capability.json"));
            Assert.That(manifest, Does.Contain("\"kind\": \"forge\""));
            Assert.That(manifest, Does.Contain("\"id\": \"github\""));
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
            Path.Join(Path.GetTempPath(), "agent-up-github-host-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.That(GithubPackerHost.Pack(directory), Is.EqualTo(directory));
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
