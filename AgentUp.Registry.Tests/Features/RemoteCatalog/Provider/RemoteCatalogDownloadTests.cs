using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Providers;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Interfaces;
using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Features.RemoteCatalog.Services;
using AgentUp.Registry.Shared.Providers;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Provider;

[TestFixture]
public sealed class RemoteCatalogDownloadTests
{
    [Test]
    public async Task DownloadFromRemoteAsync_installs_the_archive_into_the_local_registry()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-remote-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var staged = Path.Join(root, "staged");
            Directory.CreateDirectory(staged);
            File.WriteAllText(
                Path.Join(staged, "capability.json"),
                JsonSerializer.Serialize(RegistryDomain.DotnetPackage(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var archives = new PackageArchiveProvider();
            var remote = new BytesRemote(new RemotePackageBytesDto(
                RegistryDomain.DotnetId,
                RegistryDomain.DotnetVersion,
                archives.ZipDirectory(staged)));
            var local = new LocalRegistryController(new LocalRegistryService(
                new LocalRegistryDirectoryStore(new RegistryPathValidator(Path.Join(root, "registry"))),
                new CapabilityPackageController(new CapabilityPackageValidator(), new CapabilityTemplateRenderer())));
            var service = new RemoteCatalogService(local, archives, remote);

            var installed = await service.DownloadFromRemoteAsync(
                RegistryDomain.DotnetId,
                RegistryDomain.DotnetVersion,
                Path.Join(root, "download"),
                CancellationToken.None);

            Assert.That(installed.Manifest.Id, Is.EqualTo(RegistryDomain.DotnetId));
            Assert.That(local.Get(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion), Is.Not.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void DownloadFromRemoteAsync_requires_a_configured_client()
    {
        var service = new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider());

        Assert.That(
            async () => await service.DownloadFromRemoteAsync("dotnet", "1.0.0", "/tmp", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("remote"));
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

    private sealed class BytesRemote(RemotePackageBytesDto package) : IRemoteRegistryClient
    {
        public Task<IReadOnlyList<CapabilityRegistryIndexEntry>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CapabilityRegistryIndexEntry>>([]);

        public Task<RemotePackageBytesDto> DownloadAsync(string id, string version, CancellationToken cancellationToken)
            => Task.FromResult(package);

        public Task PushAsync(RemotePackageBytesDto package, string token, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
