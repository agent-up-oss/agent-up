using LocalInstaller.App.Features.Capabilities.Interfaces;
using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Providers;

public sealed class OfficialCapabilityCatalogProvider : ICapabilityCatalogProvider
{
    public const string CatalogUrlVariable = "LOCALINSTALLER_CAPABILITY_CATALOG_URL";

    private readonly CapabilityCatalogParser _parser = new();

    public async Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var configured = Environment.GetEnvironmentVariable(CatalogUrlVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return EntriesFromCatalog(await LoadConfiguredCatalogAsync(configured, cancellationToken));

        return DefaultEntries();
    }

    private async Task<CapabilityModuleCatalog> LoadConfiguredCatalogAsync(string configured, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync(uri, cancellationToken);
            return _parser.ParseCatalog(json);
        }

        var path = Uri.TryCreate(configured, UriKind.Absolute, out var fileUri) && fileUri.IsFile
            ? fileUri.LocalPath
            : configured;

        return _parser.ParseCatalog(await File.ReadAllTextAsync(path, cancellationToken));
    }

    private static IReadOnlyList<CapabilityCatalogEntry> EntriesFromCatalog(CapabilityModuleCatalog catalog) =>
        catalog.Artifacts
            .GroupBy(artifact => artifact.CapabilityId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CapabilityCatalogEntry(
                group.Key,
                DisplayName(group.Key),
                $"{DisplayName(group.Key)} capability module",
                group.OrderByDescending(artifact => artifact.Version, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<CapabilityCatalogEntry> DefaultEntries() =>
    [
        new(
            "dotnet",
            ".NET",
            "Discovers and manages .NET SDK versions.",
            [DefaultArtifact("dotnet", "10.0.x")]),
        new(
            "docker",
            "Docker",
            "Discovers Docker and manages Docker-backed services.",
            [DefaultArtifact("docker", "27.x")])
    ];

    private static CapabilityArtifact DefaultArtifact(string capabilityId, string version) =>
        new(
            capabilityId,
            version,
            new Uri($"https://example.invalid/localinstaller/capability-{capabilityId}.zip"),
            "0000000000000000000000000000000000000000000000000000000000000000");

    private static string DisplayName(string id) =>
        id.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? ".NET" : id[..1].ToUpperInvariant() + id[1..];
}
