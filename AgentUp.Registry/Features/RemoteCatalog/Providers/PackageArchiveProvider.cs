using System.IO.Compression;

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
        var destination = Path.Join(destinationRoot, Encode(id), Encode(version));
        Directory.CreateDirectory(destination);
        using var stream = new MemoryStream(archive);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        zip.ExtractToDirectory(destination, overwriteFiles: true);
        return destination;
    }

    private static string Encode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\') || value.Contains('\0'))
            throw new InvalidOperationException("Capability package id and version must not contain path separators.");
        return value;
    }
}
