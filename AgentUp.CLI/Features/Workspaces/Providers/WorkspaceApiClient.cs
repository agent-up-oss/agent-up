using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public sealed class WorkspaceApiClient
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public WorkspaceApiClient(HttpClient http) => _http = http;

    public async Task<WorkspaceDto?> RegisterAsync(RegisterWorkspaceRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/workspaces", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(Options);
    }

    public async Task<List<WorkspaceDto>> ListAsync()
    {
        var result = await _http.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces", Options);
        return result ?? [];
    }

    public async Task StartWorkspaceAsync(string id)
    {
        var response = await _http.PostAsync($"/api/workspaces/{id}/start", null);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    public async Task StopWorkspaceAsync(string id)
    {
        var response = await _http.PostAsync($"/api/workspaces/{id}/stop", null);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
            return body;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _ = ex;
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}
