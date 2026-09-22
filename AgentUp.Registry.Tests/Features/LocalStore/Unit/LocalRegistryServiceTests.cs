using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.LocalStore.Unit;

[TestFixture]
public sealed class LocalRegistryServiceTests
{
    [Test]
    public void Install_rejects_an_invalid_manifest()
    {
        var store = new FakeStore(RegistryDomain.DotnetPackage() with { Id = "" });
        var service = new LocalRegistryService(
            store,
            new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer()));

        Assert.That(() => service.Install("/staged"), Throws.InvalidOperationException.With.Message.Contains("id"));
        Assert.That(store.Wrote, Is.False);
    }

    [Test]
    public void List_maps_index_entries_to_dtos()
    {
        var store = new FakeStore(RegistryDomain.DotnetPackage());
        var service = new LocalRegistryService(
            store,
            new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer()));

        var list = service.List();

        Assert.That(list.Packages.Single().Id, Is.EqualTo(RegistryDomain.DotnetId));
        Assert.That(list.Packages.Single().DisplayName, Is.EqualTo(RegistryDomain.DotnetDisplayName));
    }

    private sealed class FakeStore(CapabilityPackageManifest manifest) : ILocalRegistryDirectoryStore
    {
        public bool Wrote { get; private set; }

        public CapabilityRegistryIndex ReadIndex()
            => new()
            {
                Packages =
                [
                    new CapabilityRegistryIndexEntry(
                        manifest.Id, manifest.Version, manifest.DisplayName, manifest.Publisher, manifest.Kind)
                ]
            };

        public LocalRegistryPackageDto? ReadPackage(string id, string version)
            => new(manifest, "/packages/" + id + "/" + version, true);

        public LocalRegistryPackageDto ReadStagedPackage(string packageDirectory)
            => new(manifest, packageDirectory, false);

        public IReadOnlyList<LocalRegistryPackageDto> ReadPackages()
            => [new(manifest, "/packages/" + manifest.Id + "/" + manifest.Version, true)];

        public void WritePackage(string packageDirectory) => Wrote = true;
    }
}
