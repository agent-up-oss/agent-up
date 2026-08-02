namespace AgentUp.Server.Features.Audit.Models;

public sealed record AuditArtifact(
    string ArtifactId,
    string EventId,
    string Kind,
    string MimeType,
    string FileName,
    long SizeBytes,
    DateTimeOffset CreatedAt);
