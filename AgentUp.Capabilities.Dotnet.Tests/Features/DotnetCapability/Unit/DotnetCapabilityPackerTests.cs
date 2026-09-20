using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Unit;

[TestFixture]
public sealed class DotnetCapabilityPackerTests
{
    [Test]
    public void Manifest_declares_the_dotnet_application_contract()
    {
        var manifest = new DotnetCapabilityPacker().Manifest();

        Assert.That(manifest.Id, Is.EqualTo("dotnet"));
        Assert.That(manifest.Kind, Is.EqualTo("runtime"));
        Assert.That(manifest.Parameters["project"].Required, Is.True);
        Assert.That(manifest.Launch!.Command, Is.EqualTo("dotnet"));
    }

    [Test]
    public void DefaultNix_pins_the_dotnet_sdk_package()
    {
        Assert.That(new DotnetCapabilityPacker().DefaultNix(), Does.Contain("dotnet-sdk_10"));
    }
}
