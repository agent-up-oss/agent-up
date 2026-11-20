namespace AgentUp.Server.Features.Forge.Models;

/// <summary>
/// What an OAuth sign-in leaves the Server with. Hosts that expire access tokens (Forgejo after
/// an hour by default) also hand back a refresh token, and without it a workspace silently loses
/// its host once the hour is up.
/// </summary>
public sealed record ForgeOAuthGrant(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt)
{
    /// <summary>Refresh a minute early, so a token never expires between the check and the call.</summary>
    public bool NeedsRefresh(DateTimeOffset now) =>
        RefreshToken is not null && ExpiresAt is { } expires && expires <= now.AddMinutes(1);
}
