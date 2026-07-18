using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class NixOsCapabilityModuleStore(CapabilityInventoryFileProvider inventory) : ICapabilityModuleStore
{
    public async Task<IReadOnlyList<InstalledCapabilityModule>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var source = CapabilityInventoryFileProvider.InventoryPathCandidates().FirstOrDefault(File.Exists)
                     ?? "NixOS capability inventory";

        return (await inventory.LoadAllAsync(cancellationToken))
            .Where(entry => entry.Versions.Count > 0)
            .Select(entry => new InstalledCapabilityModule(
                entry.Id,
                DisplayName(entry.Id),
                "Declared by NixOS or Home Manager.",
                entry.Versions[0],
                entry.Versions
                    .Select(version => new CapabilityInstalledVersion(
                        entry.Id,
                        version,
                        source,
                        CapabilityVersionSource.AgentUpManaged,
                        version == entry.Versions[0]))
                    .ToList()))
            .OrderBy(module => module.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task SaveAsync(IReadOnlyList<InstalledCapabilityModule> modules, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Capability modules are managed by NixOS or Home Manager configuration.");

    private static string DisplayName(string id)
        => id.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? ".NET"
            : id.Equals("docker", StringComparison.OrdinalIgnoreCase) ? "Docker"
            : id;
}
