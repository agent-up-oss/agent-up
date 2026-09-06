namespace AgentUp.Desktop.Features.Audit.DTOs;

public sealed record ApplicationAuditPageDto(
    IReadOnlyList<ApplicationAuditEventDto> Items,
    DateTimeOffset? NextBefore,
    string? NextBeforeEventId);
