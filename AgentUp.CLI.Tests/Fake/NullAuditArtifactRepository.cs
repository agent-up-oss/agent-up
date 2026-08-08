using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class NullAuditArtifactRepository : IAuditArtifactRepository
{
    public Task<AuditArtifact> SaveAsync(string eventId, string kind, string mimeType, byte[] bytes, CancellationToken cancellationToken)
        => Task.FromResult(new AuditArtifact(Guid.NewGuid().ToString("N"), eventId, kind, mimeType, string.Empty, 0, DateTimeOffset.UtcNow));

    public Task<(AuditArtifact Metadata, byte[] Bytes)?> LoadAsync(string artifactId, CancellationToken cancellationToken)
        => Task.FromResult<(AuditArtifact, byte[])?>(null);
}
