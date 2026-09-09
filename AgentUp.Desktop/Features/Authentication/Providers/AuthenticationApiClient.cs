using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public sealed class AuthenticationApiClient(HttpClient http)
{
    public async Task<bool> IsAuthenticationRequiredAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetFromJsonAsync<AuthenticationResponse>("/api/auth/status", cancellationToken);
        return response?.AuthenticationRequired ?? throw new InvalidOperationException("Invalid authentication status response.");
    }

    public async Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
    {
        if (http.BaseAddress is not null)
            SecureServerUrlProvider.EnsureCredentialTransportAllowed(http.BaseAddress);

        using var response = await http.PostAsJsonAsync("/api/auth/login", new { password }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The admin password is incorrect.");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(cancellationToken);
        return result?.AccessToken ?? throw new InvalidOperationException("The server did not return an access token.");
    }
}
