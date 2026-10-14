using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Forge.Interfaces;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthExchangeProvider(IForgeOAuthSettings settings, HttpMessageHandler? handler = null)
    : IForgeOAuthExchange
{
    public async Task<string> ExchangeAsync(string code, CancellationToken cancellationToken)
    {
        var configuration = settings.Read();
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var response = await client.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
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
            throw new InvalidOperationException("GitHub did not return an access token.");
        return token.GetString() ?? throw new InvalidOperationException("GitHub returned an empty access token.");
    }
}
