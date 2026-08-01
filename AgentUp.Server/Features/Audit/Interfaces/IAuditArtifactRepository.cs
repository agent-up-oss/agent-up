using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Interfaces;

public interface IAuditArtifactRepository
{
    Task<AuditArtifact> SaveAsync(
        string eventId,
        string kind,
        string mimeType,
        byte[] bytes,
        CancellationToken cancellationToken);

    Task<(AuditArtifact Metadata, byte[] Bytes)?> LoadAsync(
        string artifactId,
        CancellationToken cancellationToken);
}
