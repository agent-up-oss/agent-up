using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryAuditArtifactRepository : IAuditArtifactRepository
{
    private readonly Dictionary<string, (AuditArtifact Metadata, byte[] Bytes)> _artifacts = [];

    public Task<AuditArtifact> SaveAsync(
        string eventId,
        string kind,
        string mimeType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var artifact = new AuditArtifact(
            Guid.NewGuid().ToString("N"),
            eventId,
            kind,
            mimeType,
            "artifact.png",
            bytes.LongLength,
            DateTimeOffset.UtcNow);
        _artifacts[artifact.ArtifactId] = (artifact, bytes);
        return Task.FromResult(artifact);
    }

    public Task<(AuditArtifact Metadata, byte[] Bytes)?> LoadAsync(
        string artifactId,
        CancellationToken cancellationToken)
        => Task.FromResult(_artifacts.TryGetValue(artifactId, out var value) ? value : ((AuditArtifact, byte[])?)null);
}
