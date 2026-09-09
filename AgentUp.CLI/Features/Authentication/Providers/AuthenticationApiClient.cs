using System.Net;
using System.Net.Http.Json;
using AgentUp.CLI.Features.Authentication.DTOs;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Authentication.Providers;

public sealed class AuthenticationApiClient(HttpClient http)
{
    public async Task<bool> IsAuthenticationRequiredAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("/api/auth/status", cancellationToken);
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        return result?.AuthenticationRequired
               ?? throw new InvalidOperationException("Invalid authentication status response.");
    }

    public async Task<string> LoginAsync(string password, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("/api/auth/login", new { password }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The admin password is incorrect.");
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        return result?.AccessToken ?? throw new InvalidOperationException("The server did not return an access token.");
    }
}
