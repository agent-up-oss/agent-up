namespace AgentUp.Server.Features.Audit.DTOs;

public sealed record AuditEventDto(
    string EventId,
    DateTimeOffset Timestamp,
    string Kind,
    string Source,
    string Action,
    string Outcome,
    string? WorkspaceId,
    string? RepositoryPath,
    string? WorktreePath,
    string? WorkdirId,
    string? Branch,
    string? Commit,
    bool? Dirty,
    IReadOnlyDictionary<string, string> Details,
    IReadOnlyList<string> ArtifactIds);
