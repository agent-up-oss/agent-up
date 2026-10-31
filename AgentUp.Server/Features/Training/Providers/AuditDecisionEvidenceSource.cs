using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Training.Interfaces;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>
/// Reads an agent's capture out of the audit store so the backlog can take its own copy. This is
/// the only place the two features meet: afterwards the decision owns its picture outright.
/// </summary>
public sealed class AuditDecisionEvidenceSource(AuditController audit) : IDecisionEvidenceSource
{
    public async Task<(string MimeType, byte[] Bytes)?> ReadAsync(string artifactId, CancellationToken cancellationToken)
    {
        var artifact = await audit.LoadArtifactAsync(artifactId, includeImage: true, cancellationToken);
        return artifact?.ImageBase64 is null
            ? null
            : (artifact.MimeType, Convert.FromBase64String(artifact.ImageBase64));
    }
}
