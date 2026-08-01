namespace AgentUp.Server.Features.Audit.Models;

public sealed record AuditIdentity(
    string? RepositoryPath,
    string? WorktreePath,
    string? WorkdirId,
    string? Branch,
    string? Commit,
    bool? Dirty);
