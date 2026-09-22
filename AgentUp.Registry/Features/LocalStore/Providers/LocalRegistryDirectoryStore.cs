using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.LocalStore.DTOs;
using AgentUp.Registry.Features.LocalStore.Interfaces;
using AgentUp.Registry.Shared.Interfaces;

namespace AgentUp.Registry.Features.LocalStore.Providers;

public sealed class LocalRegistryDirectoryStore(IRegistryPathValidator paths) : ILocalRegistryDirectoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CapabilityRegistryIndex ReadIndex()
    {
        var path = paths.ResolveIndexPath();
        if (!File.Exists(path))
            return new CapabilityRegistryIndex();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CapabilityRegistryIndex>(json, Json) ?? new CapabilityRegistryIndex();
    }

    /// <summary>
    /// Reads a package the index lists. An id and version that name no index entry read as
    /// absent, so a directory sitting under the registry root that the registry never recorded
    /// is not served.
    /// </summary>
    /// <remarks>
    /// The directory is built from the index entry rather than from the arguments, so a caller's
    /// strings only ever select a recorded entry and never reach a path. These arguments arrive
    /// from an HTTP route.
    /// </remarks>
    public LocalRegistryPackageDto? ReadPackage(string id, string version)
    {
        var entry = ReadIndex().Packages.FirstOrDefault(listed =>
            listed.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
            && listed.Version.Equals(version, StringComparison.Ordinal));
        return entry is null ? null : TryRead(paths.ResolvePackageDirectory(entry.Id, entry.Version));
    }

    public LocalRegistryPackageDto ReadStagedPackage(string packageDirectory)
        => TryRead(packageDirectory)
           ?? throw new InvalidOperationException("A capability package must contain capability.json.");

    public IReadOnlyList<LocalRegistryPackageDto> ReadPackages()
        => ReadIndex().Packages
            .Select(entry => ReadPackage(entry.Id, entry.Version))
            .Where(package => package is not null)
            .Cast<LocalRegistryPackageDto>()
            .ToArray();

    public void WritePackage(string packageDirectory)
    {
        var manifestPath = Path.Join(packageDirectory, "capability.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("A capability package must contain capability.json.");

        var manifest = JsonSerializer.Deserialize<CapabilityPackageManifest>(File.ReadAllText(manifestPath), Json)
            ?? throw new InvalidOperationException("Capability package manifest is empty.");

        var destination = paths.ResolvePackageDirectory(manifest.Id, manifest.Version);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(packageDirectory))
            File.Copy(file, Path.Join(destination, Path.GetFileName(file)), overwrite: true);

        WriteIndex(manifest);
    }

    private static LocalRegistryPackageDto? TryRead(string directory)
    {
        var manifestPath = Path.Join(directory, "capability.json");
        if (!File.Exists(manifestPath))
            return null;

        var manifest = JsonSerializer.Deserialize<CapabilityPackageManifest>(File.ReadAllText(manifestPath), Json);
        if (manifest is null)
            return null;

        return new LocalRegistryPackageDto(manifest, directory, File.Exists(Path.Join(directory, "default.nix")));
    }

    private void WriteIndex(CapabilityPackageManifest manifest)
    {
        var index = ReadIndex();
        var packages = index.Packages
            .Where(entry => !(entry.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase)
                              && entry.Version.Equals(manifest.Version, StringComparison.Ordinal)))
            .Append(new CapabilityRegistryIndexEntry(
                manifest.Id,
                manifest.Version,
                manifest.DisplayName,
                manifest.Publisher,
                manifest.Kind))
            .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Version, StringComparer.Ordinal)
            .ToArray();

        Directory.CreateDirectory(paths.RegistryRoot);
        File.WriteAllText(
            paths.ResolveIndexPath(),
            JsonSerializer.Serialize(new CapabilityRegistryIndex { SchemaVersion = "1", Packages = packages }, Json));
    }
}
