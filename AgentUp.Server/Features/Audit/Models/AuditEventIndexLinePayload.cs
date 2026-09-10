namespace AgentUp.Server.Features.Audit.Models;

internal sealed record AuditEventIndexLinePayload(
    string Id,
    DateTimeOffset Timestamp,
    string Kind,
    string? Stream,
    string? Scope,
    long Offset,
    int Length);
