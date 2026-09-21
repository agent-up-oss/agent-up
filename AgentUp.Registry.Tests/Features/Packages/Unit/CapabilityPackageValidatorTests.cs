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

    // The commit is the pin, so a package that records no hash is complete. Requiring one only
    // ever produced the same placeholder in every first-party package.
    [Test]
    public void Validate_accepts_a_nixpkgs_pin_with_no_sha256()
    {
        var manifest = RegistryDomain.DotnetPackage();
        manifest = manifest with
        {
            Nix = manifest.Nix! with { Nixpkgs = new CapabilityNixpkgsPin { Rev = RegistryDomain.NixpkgsRev } }
        };

        Assert.That(new CapabilityPackageValidator().Validate(manifest).IsValid, Is.True);
    }

    [Test]
    public void Validate_rejects_a_nixpkgs_sha256_that_is_not_an_sri_hash()
    {
        var manifest = RegistryDomain.DotnetPackage();
        manifest = manifest with
        {
            Nix = manifest.Nix! with
            {
                Nixpkgs = new CapabilityNixpkgsPin { Rev = RegistryDomain.NixpkgsRev, Sha256 = "not-a-hash" }
            }
        };

        var result = new CapabilityPackageValidator().Validate(manifest);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("SRI"));
    }

    [Test]
    public void Validate_rejects_a_missing_id()
    {
        var result = new CapabilityPackageValidator().Validate(RegistryDomain.DotnetPackage() with { Id = "" });

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("id"));
    }
}
