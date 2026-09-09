using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public sealed class WorkspaceApiClient
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public WorkspaceApiClient(HttpClient http) => _http = http;

    public async Task<WorkspaceDto?> RegisterAsync(RegisterWorkspaceRequest request)
    {
        using var response = await _http.PostAsJsonAsync("/api/workspaces", request);
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(Options);
    }

    public async Task<List<WorkspaceDto>> ListAsync()
    {
        using var response = await _http.GetAsync("/api/workspaces");
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<WorkspaceDto>>(Options) ?? [];
    }

    public async Task<WorkspaceDto?> GetByIdAsync(string id)
    {
        using var response = await _http.GetAsync($"/api/workspaces/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await ServerApiResponseGuard.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(Options);
    }

    public async Task StartWorkspaceAsync(string id)
    {
        using var response = await _http.PostAsync($"/api/workspaces/{id}/start", null);
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
    }

    public async Task StopWorkspaceAsync(string id)
    {
        using var response = await _http.PostAsync($"/api/workspaces/{id}/stop", null);
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
    }

    public async Task DeleteWorkspaceAsync(string id)
    {
        using var response = await _http.DeleteAsync($"/api/workspaces/{id}");
        await ServerApiResponseGuard.EnsureSuccessAsync(response);
    }
}
