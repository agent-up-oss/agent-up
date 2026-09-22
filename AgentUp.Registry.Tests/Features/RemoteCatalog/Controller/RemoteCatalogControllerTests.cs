using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Features.RemoteCatalog.Controllers;
using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Features.RemoteCatalog.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Controller;

[TestFixture]
public sealed class RemoteCatalogControllerTests
{
    [Test]
    public void ListLocal_returns_the_service_catalog()
    {
        var controller = new RemoteCatalogController(
            new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider(RegistryDomain.Paths(RegistryDomain.RegistryRoot))));

        Assert.That(controller.ListLocal().Packages, Is.Empty);
    }

    [Test]
    public void Export_throws_when_the_package_is_missing()
    {
        var controller = new RemoteCatalogController(
            new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider(RegistryDomain.Paths(RegistryDomain.RegistryRoot))));

        Assert.That(
            () => controller.Export(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion),
            Throws.InvalidOperationException.With.Message.Contains("not in the local registry"));
    }

    private static LocalRegistryController CreateLocal()
        => new(new LocalRegistryService(
            new EmptyStore(),
            new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer())));

    private sealed class EmptyStore : ILocalRegistryDirectoryStore
    {
        public CapabilityRegistryIndex ReadIndex() => new();
        public LocalRegistryPackageDto? ReadPackage(string id, string version) => null;
        public LocalRegistryPackageDto ReadStagedPackage(string packageDirectory)
            => throw new InvalidOperationException("no staged package");
        public IReadOnlyList<LocalRegistryPackageDto> ReadPackages() => [];
        public void WritePackage(string packageDirectory)
        {
        }
    }
}
