namespace AgentUp.Server.Features.Audit.DTOs;

public sealed record AuditArtifactResult(
    string ArtifactId,
    string EventId,
    string Kind,
    string MimeType,
    long SizeBytes,
    string? ImageBase64);
