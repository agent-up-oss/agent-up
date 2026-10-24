using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Forge.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class HostConnectionPresenceProvider(
    IAgentWorkspaceSettingsRepository settings,
    ForgeController? forge = null) : IHostConnectionPresence
{
    public bool IsPresent() =>
        settings.List().Any(item => item.HostConnectionAuthorized) || forge?.HasAnyCredential() == true;

    public bool IsPresent(string workspaceId) =>
        settings.Get(workspaceId).HostConnectionAuthorized || forge?.HasCredential(workspaceId) == true;
}
