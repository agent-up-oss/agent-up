namespace AgentUp.Server.Features.Audit.DTOs;

public sealed record AuditEventPageDto(
    IReadOnlyList<AuditEventDto> Items,
    DateTimeOffset? NextBefore,
    string? NextBeforeEventId)
{
    public static AuditEventPageDto Create(IReadOnlyList<AuditEventDto> items, int limit)
    {
        var last = items.Count == limit ? items[^1] : null;
        return new AuditEventPageDto(items, last?.Timestamp, last?.EventId);
    }
}
