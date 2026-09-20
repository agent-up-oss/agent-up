namespace AgentUp.Server.Features.Entitlements.DTOs;

public sealed record EntitlementLimitDto(long? Max, long? Used);
