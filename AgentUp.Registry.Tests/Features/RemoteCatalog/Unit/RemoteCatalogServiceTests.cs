using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.LocalStore.Services;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Registry.Features.Packages.Services;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Interfaces;
using AgentUp.Registry.Features.RemoteCatalog.Providers;
using AgentUp.Registry.Features.RemoteCatalog.Services;
using AgentUp.Registry.Tests.Support;

namespace AgentUp.Registry.Tests.Features.RemoteCatalog.Unit;

[TestFixture]
public sealed class RemoteCatalogServiceTests
{
    [Test]
    public async Task RefreshFromRemoteAsync_returns_remote_entries()
    {
        var remote = new FakeRemote(
            [
                new CapabilityRegistryIndexEntry(
                    RegistryDomain.DotnetId,
                    RegistryDomain.DotnetVersion,
                    RegistryDomain.DotnetDisplayName,
                    RegistryDomain.Publisher,
                    "runtime")
            ]);
        var service = new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider(RegistryDomain.Paths(RegistryDomain.RegistryRoot)), remote);

        var list = await service.RefreshFromRemoteAsync(CancellationToken.None);

        Assert.That(list.Packages.Single().Id, Is.EqualTo(RegistryDomain.DotnetId));
    }

    [Test]
    public void RefreshFromRemoteAsync_requires_a_configured_client()
    {
        var service = new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider(RegistryDomain.Paths(RegistryDomain.RegistryRoot)));

        Assert.That(
            async () => await service.RefreshFromRemoteAsync(CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("remote"));
    }

    [Test]
    public void PushToRemoteAsync_requires_a_local_package()
    {
        var remote = new CapturingRemote();
        var service = new RemoteCatalogService(CreateLocal(), new PackageArchiveProvider(RegistryDomain.Paths(RegistryDomain.RegistryRoot)), remote);

        Assert.That(
            async () => await service.PushToRemoteAsync(RegistryDomain.DotnetId, RegistryDomain.DotnetVersion, "token", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("not in the local registry"));
        Assert.That(remote.Pushed, Is.False);
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

    private sealed class FakeRemote(IReadOnlyList<CapabilityRegistryIndexEntry> packages) : IRemoteRegistryClient
    {
        public Task<IReadOnlyList<CapabilityRegistryIndexEntry>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(packages);

        public Task<RemotePackageBytesDto> DownloadAsync(string id, string version, CancellationToken cancellationToken)
            => throw new InvalidOperationException("not used");

        public Task PushAsync(RemotePackageBytesDto package, string token, CancellationToken cancellationToken)
            => throw new InvalidOperationException("not used");
    }

    private sealed class CapturingRemote : IRemoteRegistryClient
    {
        public bool Pushed { get; private set; }

        public Task<IReadOnlyList<CapabilityRegistryIndexEntry>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CapabilityRegistryIndexEntry>>([]);

        public Task<RemotePackageBytesDto> DownloadAsync(string id, string version, CancellationToken cancellationToken)
            => throw new InvalidOperationException("not used");

        public Task PushAsync(RemotePackageBytesDto package, string token, CancellationToken cancellationToken)
        {
            Pushed = true;
            return Task.CompletedTask;
        }
    }
}
