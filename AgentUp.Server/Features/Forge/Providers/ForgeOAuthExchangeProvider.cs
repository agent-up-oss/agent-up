using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthExchangeProvider(IForgeOAuthSettings settings, HttpMessageHandler? handler = null)
    : IForgeOAuthExchange
{
    public async Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenUrl))
            throw new InvalidOperationException(ForgeHostingCopy.OAuthMissingDetail);

        var configuration = settings.Read();
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var response = await client.PostAsJsonAsync(
            tokenUrl,
            new
            {
                client_id = configuration.ClientId,
                client_secret = configuration.ClientSecret,
                code,
                redirect_uri = configuration.RedirectUri
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("access_token", out var token))
            throw new InvalidOperationException(ForgeHostingCopy.MissingToken);
        var value = token.GetString();
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(ForgeHostingCopy.EmptyToken);
        return value;
    }
}
