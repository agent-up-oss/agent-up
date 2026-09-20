using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Abstractions.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityPackageManifestTests
{
    [Test]
    public void Package_manifest_defaults_are_empty_collections()
    {
        var manifest = new CapabilityPackageManifest();

        Assert.That(manifest.SchemaVersion, Is.EqualTo("1"));
        Assert.That(manifest.Kind, Is.Empty);
        Assert.That(manifest.Module, Is.Empty);
        Assert.That(manifest.Parameters, Is.Empty);
    }

    [Test]
    public void Registry_index_entry_carries_kind_instead_of_roles()
    {
        var entry = new CapabilityRegistryIndexEntry("dotnet", "1.0.0", ".NET", "agent-up", "runtime");

        Assert.That(entry.Kind, Is.EqualTo("runtime"));
        Assert.That(entry.Id, Is.EqualTo("dotnet"));
    }
}
