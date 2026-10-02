namespace AgentUp.Desktop.Features.Entitlements.DTOs;

public sealed record EntitlementsDocumentDto(
    string? ApiVersion,
    string? ConnectionId,
    string? Subject,
    string? Source,
    string? Edition,
    string? DisplayName,
    string? Billing,
    string? Revision,
    string? ExpiresAt,
    IReadOnlyDictionary<string, EntitlementFeatureDocument>? Features,
    IReadOnlyDictionary<string, EntitlementLimitDocument>? Limits);
