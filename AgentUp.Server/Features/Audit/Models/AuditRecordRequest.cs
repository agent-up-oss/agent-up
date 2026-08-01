namespace AgentUp.Server.Features.Audit.Models;

public sealed record AuditRecordRequest(
    string Kind,
    string Source,
    string Action,
    string Outcome,
    string? WorkspaceId,
    IReadOnlyDictionary<string, string>? Details = null,
    IReadOnlyList<string>? ArtifactIds = null);
