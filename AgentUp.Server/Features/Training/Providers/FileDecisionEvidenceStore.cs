using System.Text.Json;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>
/// The backlog's own picture store, under the Server's data directory. Nothing here is reachable
/// from a workspace, so cleaning a checkout or moving a branch cannot take a decision's evidence
/// with it.
/// </summary>
public sealed class FileDecisionEvidenceStore : IDecisionEvidenceStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _root;

    public FileDecisionEvidenceStore(string dataDirectory)
    {
        _root = Path.GetFullPath(Path.Join(dataDirectory, "training", "evidence"));
        System.IO.Directory.CreateDirectory(_root);
    }

    public async Task<DecisionEvidenceImage> SaveAsync(
        string sourceArtifactId,
        string mimeType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var image = new DecisionEvidenceImage(Guid.NewGuid().ToString("N"), sourceArtifactId, mimeType);
        var directory = DirectoryFor(image.EvidenceId);
        System.IO.Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Join(directory, FileName(mimeType)), bytes, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Join(directory, "evidence.json"),
            JsonSerializer.Serialize(image, Json),
            cancellationToken);
        return image;
    }

    public async Task<(DecisionEvidenceImage Image, byte[] Bytes)?> LoadAsync(
        string evidenceId,
        CancellationToken cancellationToken)
    {
        // The id arrives from GET /api/training/evidence/{evidenceId}: a caller can send anything.
        if (!TryDirectoryFor(evidenceId, out var directory))
            return null;
        var descriptor = Path.Join(directory, "evidence.json");
        if (!File.Exists(descriptor))
            return null;

        var image = JsonSerializer.Deserialize<DecisionEvidenceImage>(
            await File.ReadAllTextAsync(descriptor, cancellationToken), Json);
        if (image is null)
            return null;

        var file = Path.Join(directory, FileName(image.MimeType));
        return File.Exists(file)
            ? (image, await File.ReadAllBytesAsync(file, cancellationToken))
            : null;
    }

    private string DirectoryFor(string evidenceId) => Path.Join(_root, evidenceId);

    /// <summary>
    /// Ids are minted here as 32 hex digits. A read accepts only that shape, and the resolved
    /// directory must still sit under the root, so a caller's id cannot reach another path.
    /// </summary>
    private bool TryDirectoryFor(string evidenceId, out string directory)
    {
        directory = "";
        if (evidenceId.Length != 32 || !evidenceId.All(Uri.IsHexDigit))
            return false;
        var resolved = Path.GetFullPath(Path.Join(_root, evidenceId));
        if (!resolved.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;
        directory = resolved;
        return true;
    }

    private static string FileName(string mimeType)
        => "evidence" + (mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".bin");
}
