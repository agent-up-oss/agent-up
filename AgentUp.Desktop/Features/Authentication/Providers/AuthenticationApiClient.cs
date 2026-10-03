using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public sealed class AuthenticationApiClient(HttpClient http)
{
    public async Task<bool> IsAuthenticationRequiredAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetFromJsonAsync<AuthenticationResponse>("/api/auth/status", cancellationToken);
        return response?.AuthenticationRequired ?? throw new InvalidOperationException("Invalid authentication status response.");
    }

    public async Task<ConnectionSource> ResolveConnectionAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = SecureServerUrlProvider.Normalize(
            ServerSessionProvider.CurrentUri(http)
            ?? http.BaseAddress
            ?? throw new InvalidOperationException("The Server URL is not configured."));
        using var response = await http.GetAsync("/api/connection", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return ConnectionSourceParser.LegacySelfHosted(baseUrl, await IsAuthenticationRequiredAsync(cancellationToken));

        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<ConnectionMetadataResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        if (document is null)
            throw new InvalidOperationException("This Server did not return a connection document.");
        return ConnectionSourceParser.Parse(baseUrl, document);
    }

    public async Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
    {
        var serverUri = ServerSessionProvider.CurrentUri(http);
        if (serverUri is not null)
            SecureServerUrlProvider.EnsureCredentialTransportAllowed(serverUri);

        using var response = await http.PostAsJsonAsync("/api/auth/login", new { password }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The admin password is incorrect.");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(cancellationToken);
        return result?.AccessToken ?? throw new InvalidOperationException("The server did not return an access token.");
    }
}
