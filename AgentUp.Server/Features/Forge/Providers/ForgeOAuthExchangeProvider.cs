using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthExchangeProvider(IForgeOAuthSettings settings, HttpMessageHandler? handler = null, TimeProvider? time = null)
    : IForgeOAuthExchange
{
    public async Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken)
        => (await ExchangeGrantAsync(code, tokenUrl, cancellationToken)).AccessToken;

    public async Task<string> ExchangeAsync(string code, string tokenUrl, string redirectUri, CancellationToken cancellationToken)
        => (await GrantAsync(code, tokenUrl, redirectUri, cancellationToken)).AccessToken;

    public Task<ForgeOAuthGrant> ExchangeGrantAsync(string code, string tokenUrl, CancellationToken cancellationToken)
        => GrantAsync(code, tokenUrl, null, cancellationToken);

    private Task<ForgeOAuthGrant> GrantAsync(string code, string tokenUrl, string? redirectUri, CancellationToken cancellationToken)
        => RequestAsync(tokenUrl, configuration => new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = configuration.ClientId,
            ["client_secret"] = configuration.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri ?? configuration.RedirectUri
        }, cancellationToken);

    public Task<ForgeOAuthGrant> RefreshAsync(string refreshToken, string tokenUrl, CancellationToken cancellationToken)
        => RequestAsync(tokenUrl, configuration => new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = configuration.ClientId,
            ["client_secret"] = configuration.ClientSecret,
            ["refresh_token"] = refreshToken
        }, cancellationToken);

    private async Task<ForgeOAuthGrant> RequestAsync(
        string tokenUrl,
        Func<ForgeOAuthConfiguration, Dictionary<string, string>> body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenUrl))
            throw new InvalidOperationException(ForgeHostingCopy.OAuthMissingDetail);

        var configuration = settings.Read();
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        // RFC 6749 sections 4.1.3 and 6: a form body naming the grant. Forgejo refuses a request
        // without grant_type; GitHub accepts this shape as well.
        using var form = new FormUrlEncodedContent(body(configuration));
        using var response = await client.PostAsync(tokenUrl, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"The host refused the sign-in code ({(int)response.StatusCode}): {Trim(await response.Content.ReadAsStringAsync(cancellationToken))}");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("access_token", out var token))
            throw new InvalidOperationException(ForgeHostingCopy.MissingToken);
        var value = token.GetString();
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(ForgeHostingCopy.EmptyToken);
        var refresh = document.RootElement.TryGetProperty("refresh_token", out var refreshValue) ? refreshValue.GetString() : null;
        DateTimeOffset? expires = document.RootElement.TryGetProperty("expires_in", out var seconds) && seconds.TryGetInt32(out var lifetime) && lifetime > 0
            ? (time ?? TimeProvider.System).GetUtcNow().AddSeconds(lifetime)
            : null;
        return new ForgeOAuthGrant(value, string.IsNullOrEmpty(refresh) ? null : refresh, expires);
    }

    private static string Trim(string body) => body.Length <= 300 ? body.Trim() : body[..300].Trim() + "…";
}
