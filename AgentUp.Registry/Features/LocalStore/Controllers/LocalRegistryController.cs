using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Services;

namespace AgentUp.Registry.Features.LocalStore.Controllers;

public sealed class LocalRegistryController(LocalRegistryService registry)
{
    public LocalRegistryIndexDto List() => registry.List();

    public IReadOnlyList<LocalRegistryPackageDto> ListPackages() => registry.ListPackages();

    public LocalRegistryPackageDto? Get(string id, string version) => registry.Get(id, version);

    public LocalRegistryPackageDto Install(string packageDirectory) => registry.Install(packageDirectory);
}
