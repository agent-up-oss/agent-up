using System.Text.Json;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Repositories;

public sealed class FileAuditArtifactRepository : IAuditArtifactRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _root;

    public FileAuditArtifactRepository(string dataDir)
    {
        _root = Path.GetFullPath(Path.Join(dataDir, "audit", "artifacts"));
        Directory.CreateDirectory(_root);
    }

    public async Task<AuditArtifact> SaveAsync(
        string eventId,
        string kind,
        string mimeType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var artifactId = Guid.NewGuid().ToString("N");
        var dir = ArtifactDirectory(artifactId);
        Directory.CreateDirectory(dir);
        var extension = mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".bin";
        var fileName = "artifact" + extension;
        var filePath = Path.Join(dir, fileName);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

        var metadata = new AuditArtifact(
            artifactId,
            eventId,
            kind,
            mimeType,
            fileName,
            bytes.LongLength,
            DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(
            Path.Join(dir, "metadata.json"),
            JsonSerializer.Serialize(metadata, Options),
            cancellationToken);
        return metadata;
    }

    public async Task<(AuditArtifact Metadata, byte[] Bytes)?> LoadAsync(
        string artifactId,
        CancellationToken cancellationToken)
    {
        var dir = ArtifactDirectory(artifactId);
        var metadataPath = Path.Join(dir, "metadata.json");
        if (!File.Exists(metadataPath))
            return null;

        var metadata = JsonSerializer.Deserialize<AuditArtifact>(
            await File.ReadAllTextAsync(metadataPath, cancellationToken),
            Options);
        if (metadata is null)
            return null;

        var path = Path.Join(dir, metadata.FileName);
        if (!File.Exists(path))
            return null;

        return (metadata, await File.ReadAllBytesAsync(path, cancellationToken));
    }

    private string ArtifactDirectory(string artifactId)
    {
        if (artifactId.Any(ch => !char.IsAsciiHexDigit(ch)))
            throw new InvalidOperationException("Invalid artifact id.");

        var path = Path.GetFullPath(Path.Join(_root, artifactId));
        var root = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!path.StartsWith(root, comparison))
            throw new InvalidOperationException("Artifact path escaped the audit root.");

        return path;
    }
}
