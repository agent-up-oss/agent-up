namespace AgentUp.Desktop.Features.Audit.DTOs;

public sealed record ApplicationAuditEventDto(
    string EventId,
    DateTimeOffset Timestamp,
    string Action,
    string Outcome,
    IReadOnlyDictionary<string, string> Details);
