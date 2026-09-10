namespace AgentUp.Server.Features.Audit.Models;

internal sealed record AuditEventIndexEntry(
    string EventId,
    DateTimeOffset Timestamp,
    string Kind,
    string? Stream,
    string? Scope,
    long Offset,
    int Length);
