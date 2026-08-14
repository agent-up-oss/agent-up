using System.Text.Json;
using LocalInstaller.App.Features.Capabilities.Interfaces;
using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Providers;

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
