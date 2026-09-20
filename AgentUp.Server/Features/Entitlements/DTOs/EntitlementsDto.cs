namespace AgentUp.Server.Features.Entitlements.DTOs;

public sealed record EntitlementsDto(
    string ApiVersion,
    string ConnectionId,
    string Subject,
    string Source,
    string Edition,
    string DisplayName,
    string Billing,
    string Revision,
    DateTimeOffset? ExpiresAt,
    IReadOnlyDictionary<string, EntitlementFeatureDto> Features,
    IReadOnlyDictionary<string, EntitlementLimitDto> Limits);
