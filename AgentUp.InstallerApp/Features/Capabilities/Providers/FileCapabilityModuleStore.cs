using System.Text.Json;
using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class FileCapabilityModuleStore(string filePath) : ICapabilityModuleStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<InstalledCapabilityModule>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return [];

        return JsonSerializer.Deserialize<List<InstalledCapabilityModule>>(
            await File.ReadAllTextAsync(filePath, cancellationToken),
            Options) ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<InstalledCapabilityModule> modules, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(modules, Options), cancellationToken);
    }
}
