using AgentUp.Server.Features.Entitlements.DTOs;
using AgentUp.Server.Features.Entitlements.Providers;

namespace AgentUp.Server.Features.Entitlements.Services;

public sealed class EntitlementsService(SelfHostedEntitlementsProvider entitlements)
{
    public EntitlementsDto ForSubject(string subject) => entitlements.ForSubject(subject);
}
