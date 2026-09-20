using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Entitlements.DTOs;

namespace AgentUp.Server.Features.Entitlements.Providers;

public sealed class SelfHostedEntitlementsProvider(IConfiguration configuration)
{
    public EntitlementsDto ForSubject(string subject)
    {
        var features = OperationPermissions.All.ToDictionary(
            permission => permission,
            _ => new EntitlementFeatureDto(true));
        var connectionId = string.IsNullOrWhiteSpace(configuration["AGENTUP_CONNECTION_ID"])
            ? "local"
            : configuration["AGENTUP_CONNECTION_ID"]!;
        var revision = string.IsNullOrWhiteSpace(configuration["AGENTUP_ENTITLEMENT_REVISION"])
            ? "community"
            : configuration["AGENTUP_ENTITLEMENT_REVISION"]!;

        return new EntitlementsDto(
            "1",
            connectionId,
            subject,
            "selfHosted",
            "community",
            "Self-hosted",
            "free",
            revision,
            ExpiresAt: null,
            features,
            new Dictionary<string, EntitlementLimitDto>());
    }
}
