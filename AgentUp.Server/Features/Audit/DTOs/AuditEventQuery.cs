namespace AgentUp.Server.Features.Audit.DTOs;

public sealed record AuditEventQuery(
    string? WorkspaceId,
    string? WorkdirId,
    string? RepositoryPath,
    string? Branch,
    string? Commit,
    string? Kind,
    string? Source,
    string? Outcome,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Limit);
