using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.LocalStore.Controller;

[TestFixture]
public sealed class LocalRegistryControllerTests
{
    [Test]
    public void List_returns_the_service_index()
    {
        var controller = CreateController(RegistryDomain.DotnetPackage());

        var list = controller.List();

        Assert.That(list.Packages.Single().Id, Is.EqualTo(RegistryDomain.DotnetId));
    }

    [Test]
    public void Get_returns_the_named_package()
    {
        var controller = CreateController(RegistryDomain.DotnetPackage());

        var package = controller.Get(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion);

        Assert.That(package!.Manifest.DisplayName, Is.EqualTo(RegistryDomain.DotnetDisplayName));
    }

    private static LocalRegistryController CreateController(CapabilityPackageManifest manifest)
        => new(new LocalRegistryService(
            new RecordingStore(manifest),
            new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer())));

    private sealed class RecordingStore(CapabilityPackageManifest manifest) : ILocalRegistryDirectoryStore
    {
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

        public IReadOnlyList<LocalRegistryPackageDto> ReadPackages() => [];

        public void WritePackage(string packageDirectory)
        {
        }
    }
}
