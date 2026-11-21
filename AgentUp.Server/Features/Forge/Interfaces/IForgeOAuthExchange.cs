using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Interfaces;

public interface IForgeOAuthExchange
{
    Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a code the host sent to <paramref name="redirectUri"/>: the token request must
    /// name the same redirect the sign-in began with, or the host refuses it.
    /// </summary>
    Task<string> ExchangeAsync(string code, string tokenUrl, string redirectUri, CancellationToken cancellationToken) =>
        ExchangeAsync(code, tokenUrl, cancellationToken);

    /// <summary>The whole grant: the access token, and the refresh token and expiry when the host sends them.</summary>
    async Task<ForgeOAuthGrant> ExchangeGrantAsync(string code, string tokenUrl, CancellationToken cancellationToken) =>
        new(await ExchangeAsync(code, tokenUrl, cancellationToken), null, null);

    /// <summary>Trades a refresh token for a new grant.</summary>
    Task<ForgeOAuthGrant> RefreshAsync(string refreshToken, string tokenUrl, CancellationToken cancellationToken) =>
        Task.FromException<ForgeOAuthGrant>(new InvalidOperationException("This sign-in cannot be refreshed."));
}
