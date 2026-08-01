namespace AgentUp.Server.Features.Audit.Models;

public sealed record AuditEvent(
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
