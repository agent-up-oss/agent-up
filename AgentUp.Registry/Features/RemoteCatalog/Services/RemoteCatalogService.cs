using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.Controllers;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Interfaces;
using AgentUp.Registry.Features.RemoteCatalog.Providers;

namespace AgentUp.Registry.Features.RemoteCatalog.Services;

public sealed class RemoteCatalogService(
    LocalRegistryController local,
    PackageArchiveProvider archives,
    IRemoteRegistryClient? remote = null)
{
    public RemoteCatalogListDto ListLocal()
        => new(local.List().Packages
            .Select(entry => new CapabilityRegistryIndexEntry(
                entry.Id, entry.Version, entry.DisplayName, entry.Publisher, entry.Kind))
            .ToArray());

    public RemotePackageBytesDto Export(string id, string version)
    {
        var package = local.Get(id, version)
            ?? throw new InvalidOperationException($"Capability package '{id}' version '{version}' is not in the local registry.");
        return new RemotePackageBytesDto(id, version, archives.ZipDirectory(package.PackageDirectory));
    }

    public LocalRegistryPackageDto Import(RemotePackageBytesDto package, string stagingRoot)
    {
        var staged = archives.Unzip(package.Archive, stagingRoot, package.Id, package.Version);
        return local.Install(staged);
    }

    public async Task<RemoteCatalogListDto> RefreshFromRemoteAsync(CancellationToken cancellationToken)
    {
        if (remote is null)
            throw new InvalidOperationException("No remote capability registry is configured.");

        return new RemoteCatalogListDto(await remote.ListAsync(cancellationToken));
    }

    public async Task<LocalRegistryPackageDto> DownloadFromRemoteAsync(
        string id,
        string version,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        if (remote is null)
            throw new InvalidOperationException("No remote capability registry is configured.");

        var bytes = await remote.DownloadAsync(id, version, cancellationToken);
        return Import(bytes, stagingRoot);
    }

    public async Task PushToRemoteAsync(string id, string version, string token, CancellationToken cancellationToken)
    {
        if (remote is null)
            throw new InvalidOperationException("No remote capability registry is configured.");

        await remote.PushAsync(Export(id, version), token, cancellationToken);
    }
}
