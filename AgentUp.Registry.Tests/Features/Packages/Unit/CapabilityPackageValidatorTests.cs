using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.Packages.Unit;

[TestFixture]
public sealed class CapabilityPackageValidatorTests
{
    [Test]
    public void Validate_accepts_the_first_party_dotnet_package()
    {
        var result = new CapabilityPackageValidator().Validate(RegistryDomain.DotnetPackage());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_rejects_an_unknown_kind()
    {
        var manifest = RegistryDomain.DotnetPackage() with { Kind = "wasm" };

        var result = new CapabilityPackageValidator().Validate(manifest);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("allowlist"));
    }

    [Test]
    public void Validate_rejects_a_missing_id()
    {
        var result = new CapabilityPackageValidator().Validate(RegistryDomain.DotnetPackage() with { Id = "" });

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("id"));
    }
}
