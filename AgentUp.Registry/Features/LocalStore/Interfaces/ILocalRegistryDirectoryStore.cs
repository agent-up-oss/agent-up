using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.DTOs;

namespace AgentUp.Registry.Features.LocalStore.Interfaces;

public interface ILocalRegistryDirectoryStore
{
    CapabilityRegistryIndex ReadIndex();
    LocalRegistryPackageDto? ReadPackage(string id, string version);
    LocalRegistryPackageDto ReadStagedPackage(string packageDirectory);
    IReadOnlyList<LocalRegistryPackageDto> ReadPackages();
    void WritePackage(string packageDirectory);
}
