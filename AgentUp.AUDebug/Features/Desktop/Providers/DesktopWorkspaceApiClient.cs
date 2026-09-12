using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.AUDebug.Features.Desktop.DTOs;
using AgentUp.AUDebug.Features.Desktop.Interfaces;
using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Desktop.Providers;

public sealed class DesktopWorkspaceApiClient : IDesktopWorkspaceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public DesktopWorkspaceApiClient(HttpClient http) => _http = http;

    public async Task StartByNameAsync(string workspaceName, string password, CancellationToken cancellationToken)
    {
        var token = await LoginAsync(password, cancellationToken);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var workspaces = await ListAsync(cancellationToken);
        var match = workspaces.FirstOrDefault(workspace =>
            workspace.DisplayName.Equals(workspaceName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new InvalidOperationException($"No workspace named '{workspaceName}' is registered on {DebugLayout.ServerUrl}.");

        using var response = await _http.PostAsync($"/api/workspaces/{Uri.EscapeDataString(match.Id)}/start", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to start '{workspaceName}': {(int)response.StatusCode} {response.ReasonPhrase}");
    }

    private async Task<string> LoginAsync(string password, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("/api/auth/login", new { password }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The admin password is incorrect.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Login failed: {(int)response.StatusCode} {response.ReasonPhrase}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.TryGetProperty("accessToken", out var token) && token.GetString() is { Length: > 0 } value)
            return value;
        throw new InvalidOperationException("The server did not return an access token.");
    }

    private async Task<IReadOnlyList<DebugWorkspaceDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("/api/workspaces", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Server authentication is required. Pass --password or set AGENTUP_ADMIN_PASSWORD.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to list workspaces: {(int)response.StatusCode} {response.ReasonPhrase}");

        return await response.Content.ReadFromJsonAsync<List<DebugWorkspaceDto>>(JsonOptions, cancellationToken) ?? [];
    }
}
