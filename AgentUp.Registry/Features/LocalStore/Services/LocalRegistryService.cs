using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Features.Packages.Controllers;

namespace AgentUp.Registry.Features.LocalStore.Services;

public sealed class LocalRegistryService(
    ILocalRegistryDirectoryStore store,
    CapabilityPackageController packages)
{
    public LocalRegistryIndexDto List()
        => new(store.ReadIndex().Packages
            .Select(entry => new LocalRegistryIndexEntryDto(
                entry.Id, entry.Version, entry.DisplayName, entry.Publisher, entry.Kind))
            .ToArray());

    public IReadOnlyList<LocalRegistryPackageDto> ListPackages() => store.ReadPackages();

    public LocalRegistryPackageDto? Get(string id, string version) => store.ReadPackage(id, version);

    public LocalRegistryPackageDto Install(string packageDirectory)
    {
        var staged = store.ReadStagedPackage(packageDirectory);
        var validation = packages.Validate(staged.Manifest);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(" ", validation.Messages));

        store.WritePackage(packageDirectory);
        return store.ReadPackage(staged.Manifest.Id, staged.Manifest.Version)
            ?? throw new InvalidOperationException("Installed capability package could not be read back.");
    }
}
