using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;
using AgentUp.Registry.Features.RemoteCatalog.Services;

namespace AgentUp.Registry.Features.RemoteCatalog.Controllers;

public sealed class RemoteCatalogController(RemoteCatalogService catalog)
{
    public RemoteCatalogListDto ListLocal() => catalog.ListLocal();

    public RemotePackageBytesDto Export(string id, string version) => catalog.Export(id, version);

    public LocalRegistryPackageDto Import(RemotePackageBytesDto package, string stagingRoot)
        => catalog.Import(package, stagingRoot);

    public Task<RemoteCatalogListDto> RefreshFromRemoteAsync(CancellationToken cancellationToken)
        => catalog.RefreshFromRemoteAsync(cancellationToken);

    public Task<LocalRegistryPackageDto> DownloadFromRemoteAsync(
        string id,
        string version,
        string stagingRoot,
        CancellationToken cancellationToken)
        => catalog.DownloadFromRemoteAsync(id, version, stagingRoot, cancellationToken);

    public Task PushToRemoteAsync(string id, string version, string token, CancellationToken cancellationToken)
        => catalog.PushToRemoteAsync(id, version, token, cancellationToken);
}
