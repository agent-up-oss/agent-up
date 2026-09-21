using System.IO.Compression;
using AgentUp.Registry.Shared.Interfaces;
using AgentUp.Registry.Shared.Providers;

namespace AgentUp.Registry.Features.RemoteCatalog.Providers;

/// <summary>
/// Packs a stored capability package into an archive and unpacks a received one into staging.
/// </summary>
/// <remarks>
/// Both directories are registry storage, so both are checked against the registry root before
/// a file is read or written. The containment check lives here as well as where the path was
/// built: a caller that assembles a directory itself - or that passes one an HTTP route named -
/// then cannot reach the filesystem with it.
/// </remarks>
public sealed class PackageArchiveProvider(IRegistryPathValidator paths)
{
    public byte[] ZipDirectory(string packageDirectory)
    {
        var directory = paths.RequireWithinRoot(packageDirectory);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        return stream.ToArray();
    }

    public string Unzip(byte[] archive, string destinationRoot, string id, string version)
    {
        var root = paths.RequireWithinRoot(destinationRoot);
        var destination = Path.Join(root, RegistryPathValidator.Segment(id), RegistryPathValidator.Segment(version));
        Directory.CreateDirectory(destination);
        using var stream = new MemoryStream(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(destination, overwriteFiles: true);
        return destination;
    }
}
