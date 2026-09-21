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

    // Every required field, one at a time: the validator reports the field the publisher left
    // out, and a package missing several says so about each.
    [TestCase("schemaVersion")]
    [TestCase("id")]
    [TestCase("version")]
    [TestCase("displayName")]
    [TestCase("publisher")]
    [TestCase("kind")]
    public void Validate_names_the_required_field_a_package_is_missing(string field)
    {
        var manifest = RegistryDomain.DotnetPackage();
        manifest = field switch
        {
            "schemaVersion" => manifest with { SchemaVersion = "2" },
            "id" => manifest with { Id = "" },
            "version" => manifest with { Version = "  " },
            "displayName" => manifest with { DisplayName = "" },
            "publisher" => manifest with { Publisher = "" },
            _ => manifest with { Kind = "" }
        };

        var result = new CapabilityPackageValidator().Validate(manifest);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain(field));
    }

    [Test]
    public void Validate_rejects_a_manifest_that_is_not_there_at_all()
    {
        var result = new CapabilityPackageValidator().Validate(null);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("required"));
    }

    [Test]
    public void Validate_reports_every_missing_field_rather_than_the_first()
    {
        var result = new CapabilityPackageValidator()
            .Validate(RegistryDomain.DotnetPackage() with { Id = "", Publisher = "" });

        Assert.That(result.Messages, Has.Count.EqualTo(2));
    }

    [Test]
    public void Validate_accepts_an_agent_package()
    {
        var manifest = RegistryDomain.DotnetPackage() with { Id = "codex", Kind = "agent" };

        Assert.That(new CapabilityPackageValidator().Validate(manifest).IsValid, Is.True);
    }

    // The nixpkgs commit is what makes a packed shell reproducible on whatever Server enables it.
    [Test]
    public void Validate_requires_a_nixpkgs_commit_when_a_pin_is_recorded()
    {
        var manifest = RegistryDomain.DotnetPackage();
        manifest = manifest with
        {
            Nix = manifest.Nix! with { Nixpkgs = new CapabilityNixpkgsPin { Rev = "" } }
        };

        var result = new CapabilityPackageValidator().Validate(manifest);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("rev"));
    }

    [Test]
    public void Validate_accepts_a_package_with_no_nix_section()
    {
        Assert.That(
            new CapabilityPackageValidator().Validate(RegistryDomain.DotnetPackage() with { Nix = null }).IsValid,
            Is.True);
    }

    [Test]
    public void Validate_rejects_an_empty_nix_package_name()
    {
        var manifest = RegistryDomain.DotnetPackage();
        manifest = manifest with { Nix = manifest.Nix! with { Packages = ["dotnet-sdk_10", " "] } };

        var result = new CapabilityPackageValidator().Validate(manifest);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("nix.packages"));
    }
}
