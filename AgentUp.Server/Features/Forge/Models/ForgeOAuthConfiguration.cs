namespace AgentUp.Server.Features.Forge.Models;

public sealed record ForgeOAuthConfiguration(string ClientId, string ClientSecret, string RedirectUri)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}
