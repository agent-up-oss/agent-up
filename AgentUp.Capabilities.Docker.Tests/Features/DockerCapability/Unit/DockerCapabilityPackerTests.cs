using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Unit;

[TestFixture]
public sealed class DockerCapabilityPackerTests
{
    [Test]
    public void Manifest_requires_an_image_parameter()
    {
        var manifest = new DockerCapabilityPacker().Manifest();

        Assert.That(manifest.Id, Is.EqualTo("docker"));
        Assert.That(manifest.Parameters["image"].Required, Is.True);
        Assert.That(manifest.Kind, Is.EqualTo("runtime"));
    }

    [Test]
    public void DefaultNix_includes_docker()
    {
        Assert.That(new DockerCapabilityPacker().DefaultNix(), Does.Contain("pkgs.docker"));
    }
}
