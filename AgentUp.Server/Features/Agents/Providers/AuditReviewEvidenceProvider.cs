using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Audit.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Reads a reviewer's screenshot out of the audit store, so it can be attached to the review on
/// the host. Once attached, the review links the host's copy rather than the audit artifact.
/// </summary>
public sealed class AuditReviewEvidenceProvider(AuditController audit) : IReviewEvidenceSource
{
    public async Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken)
    {
        var artifact = await audit.LoadArtifactAsync(artifactId, includeImage: true, cancellationToken);
        return artifact?.ImageBase64 is null
            ? null
            : (artifact.MimeType, Convert.FromBase64String(artifact.ImageBase64));
    }
}
