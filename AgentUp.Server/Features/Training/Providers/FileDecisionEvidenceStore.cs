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
        var directory = DirectoryFor(evidenceId);
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

    /// <summary>Ids are generated here and never come from a caller, so a path cannot escape the root.</summary>
    private string DirectoryFor(string evidenceId) => Path.Join(_root, evidenceId);

    private static string FileName(string mimeType)
        => "evidence" + (mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".bin");
}
