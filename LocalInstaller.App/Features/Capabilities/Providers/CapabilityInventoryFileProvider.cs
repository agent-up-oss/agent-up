using System.Text.Json;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class CapabilityInventoryFileProvider
{
    public const string InventoryPathVariable = "AGENTUP_CAPABILITY_INVENTORY_PATH";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DeclaredCapabilityInventoryEntry>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var path = InventoryPathCandidates().FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(path))
            return [];

        return JsonSerializer.Deserialize<List<DeclaredCapabilityInventoryEntry>>(
            await File.ReadAllTextAsync(path, cancellationToken),
            Options) ?? [];
    }

    public static IReadOnlyList<string> InventoryPathCandidates()
    {
        var candidates = new List<string>();
        AddCandidate(candidates, Environment.GetEnvironmentVariable(InventoryPathVariable));
        AddCandidate(candidates, "/etc/agent-up/capabilities.json");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            AddCandidate(candidates, Path.Join(home, ".config", "agent-up", "capabilities.json"));

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || candidates.Contains(path, StringComparer.Ordinal))
            return;

        candidates.Add(path);
    }
}
