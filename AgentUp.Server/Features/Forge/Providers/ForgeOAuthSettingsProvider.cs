using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthSettingsProvider(IConfiguration configuration) : IForgeOAuthSettings
{
    public ForgeOAuthConfiguration Read()
        => new(
            configuration["AGENTUP_FORGE_CLIENT_ID"] ?? "",
            configuration["AGENTUP_FORGE_CLIENT_SECRET"] ?? "",
            configuration["AGENTUP_FORGE_REDIRECT_URI"] ?? "");
}
