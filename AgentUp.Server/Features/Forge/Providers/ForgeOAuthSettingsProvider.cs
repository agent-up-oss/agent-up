using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthSettingsProvider(IConfiguration configuration) : IForgeOAuthSettings
{
    public ForgeOAuthConfiguration Read()
        => new(
            configuration["AGENTUP_GITHUB_CLIENT_ID"] ?? "",
            configuration["AGENTUP_GITHUB_CLIENT_SECRET"] ?? "",
            configuration["AGENTUP_GITHUB_REDIRECT_URI"] ?? "");
}
