using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Models;

namespace AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

public sealed class CapabilityInventoryFileProvider
{
    public const string InventoryPathVariable = "AGENTUP_CAPABILITY_INVENTORY_PATH";
    public const string DevInventoryDirectoryName = ".agent-up-dev";
    public const string InventoryFileName = "capabilities.json";
    public const string LocalInventoryFileName = "capabilities.local.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DeclaredCapabilityInventoryEntry>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, DeclaredCapabilityInventoryEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in InventoryPathCandidates().Where(File.Exists))
        {
            var entries = JsonSerializer.Deserialize<List<DeclaredCapabilityInventoryEntry>>(
                await File.ReadAllTextAsync(path, cancellationToken),
                Options) ?? [];
            foreach (var entry in entries)
            {
                if (byId.TryGetValue(entry.Id, out var existing))
                    byId[entry.Id] = Merge(existing, entry);
                else
                    byId[entry.Id] = entry;
            }
        }

        return byId.Values.ToList();
    }

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> LoadAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        var path = InventoryPathCandidates().FirstOrDefault(File.Exists) ?? "Agent-Up capability inventory";
        var entries = await LoadAllAsync(cancellationToken);

        return entries
            .Where(entry => entry.Id.Equals(capabilityId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(entry => (entry.Versions ?? []).Select(version =>
                new CapabilityInstalledVersion(capabilityId, version, path, CapabilityVersionSource.AgentUpManaged, true)))
            .ToList();
    }

    public static IReadOnlyList<string> InventoryPathCandidates()
    {
        var candidates = new List<string>();
        AddCandidate(candidates, Environment.GetEnvironmentVariable(InventoryPathVariable));
        AddCandidate(candidates, "/etc/agent-up/capabilities.json");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            AddCandidate(candidates, Path.Join(home, ".config", "agent-up", InventoryFileName));
            AddCandidate(candidates, Path.Join(home, ".config", "agent-up", LocalInventoryFileName));
        }

        AddCandidate(candidates, FindDevInventoryPath());
        return candidates;
    }

    private static string? FindDevInventoryPath()
    {
        DirectoryInfo? directory;
        try
        {
            directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        while (directory is not null)
        {
            var path = Path.Join(directory.FullName, DevInventoryDirectoryName, InventoryFileName);
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        return null;
    }

    private static DeclaredCapabilityInventoryEntry Merge(
        DeclaredCapabilityInventoryEntry preferred,
        DeclaredCapabilityInventoryEntry fallback) =>
        new(
            preferred.Id,
            (HasValues(preferred.Versions) ? preferred.Versions : fallback.Versions) ?? [],
            string.IsNullOrWhiteSpace(preferred.Command) ? fallback.Command : preferred.Command,
            HasValues(preferred.Arguments) ? preferred.Arguments : fallback.Arguments,
            HasValues(preferred.VersionArguments) ? preferred.VersionArguments : fallback.VersionArguments);

    private static bool HasValues(IReadOnlyList<string>? values) => values is { Count: > 0 };

    private static void AddCandidate(List<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || candidates.Contains(path, StringComparer.Ordinal))
            return;

        candidates.Add(path);
    }
}
