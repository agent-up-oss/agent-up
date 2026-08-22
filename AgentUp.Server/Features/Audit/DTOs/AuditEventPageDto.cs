namespace AgentUp.Server.Features.Audit.DTOs;

public sealed record AuditEventPageDto(
    IReadOnlyList<AuditEventDto> Items,
    DateTimeOffset? NextBefore);
