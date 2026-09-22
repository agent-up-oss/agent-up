using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.RemoteCatalog.DTOs;

namespace AgentUp.Registry.Features.RemoteCatalog.Interfaces;

public interface IRemoteRegistryClient
{
    Task<IReadOnlyList<CapabilityRegistryIndexEntry>> ListAsync(CancellationToken cancellationToken);
    Task<RemotePackageBytesDto> DownloadAsync(string id, string version, CancellationToken cancellationToken);
    Task PushAsync(RemotePackageBytesDto package, string token, CancellationToken cancellationToken);
}
