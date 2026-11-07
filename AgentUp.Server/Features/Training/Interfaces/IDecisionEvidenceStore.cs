using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Interfaces;

/// <summary>
/// Where the backlog keeps the pictures behind its decisions: its own storage, outside any
/// workspace and outside git, so an image is still there years after the branch it was taken on.
/// </summary>
public interface IDecisionEvidenceStore
{
    Task<DecisionEvidenceImage> SaveAsync(string sourceArtifactId, string mimeType, byte[] bytes, CancellationToken cancellationToken);

    Task<(DecisionEvidenceImage Image, byte[] Bytes)?> LoadAsync(string evidenceId, CancellationToken cancellationToken);
}
