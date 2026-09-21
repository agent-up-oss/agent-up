using System.IO.Compression;
using AgentUp.Registry.Shared.Providers;

namespace AgentUp.Registry.Features.RemoteCatalog.Providers;

public sealed class PackageArchiveProvider
{
    public byte[] ZipDirectory(string packageDirectory)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(packageDirectory))
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        return stream.ToArray();
    }

    public string Unzip(byte[] archive, string destinationRoot, string id, string version)
    {
        var root = Path.GetFullPath(destinationRoot);
        var destination = Path.Join(root, RegistryPathValidator.Segment(id), RegistryPathValidator.Segment(version));
        Directory.CreateDirectory(destination);
        using var stream = new MemoryStream(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(destination, overwriteFiles: true);
        return destination;
    }
}
