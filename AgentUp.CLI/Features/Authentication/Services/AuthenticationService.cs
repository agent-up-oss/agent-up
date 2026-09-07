using AgentUp.CLI.Features.Authentication.Models;
using AgentUp.CLI.Features.Authentication.Providers;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Authentication.Services;

public sealed class AuthenticationService(
    AuthenticationCredentialsStore credentialsStore,
    string serverUrl,
    Func<HttpClient>? createHttpClient = null)
{
    private readonly string _normalizedServerUrl = ServerUrlNormalizer.Normalize(serverUrl);
    private readonly Func<HttpClient>? _createHttpClient = createHttpClient;

    public bool HasStoredToken() => credentialsStore.GetToken(_normalizedServerUrl) is not null;

    public async Task<bool> IsAuthenticationRequiredAsync(CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        return await new AuthenticationApiClient(http).IsAuthenticationRequiredAsync(cancellationToken);
    }

    public async Task LoginAsync(string password, CancellationToken cancellationToken = default)
    {
        using var http = CreateHttpClient();
        var token = await new AuthenticationApiClient(http).LoginAsync(password, cancellationToken);
        credentialsStore.SetToken(_normalizedServerUrl, token);
    }

    public void Logout() => credentialsStore.ClearToken(_normalizedServerUrl);

    private HttpClient CreateHttpClient()
    {
        if (_createHttpClient is not null)
            return _createHttpClient();

        return new HttpClient { BaseAddress = new Uri(_normalizedServerUrl) };
    }
}
