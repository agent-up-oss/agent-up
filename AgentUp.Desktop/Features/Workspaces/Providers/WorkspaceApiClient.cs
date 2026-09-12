using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

public sealed class WorkspaceApiClient(HttpClient http) : IWorkspaceApiProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<WorkspaceDto>> ListAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces", Options, ct);
        return result ?? [];
    }

    public async Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"/api/workspaces/{Uri.EscapeDataString(workspaceId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(Options, ct);
    }

    public async Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/api/source-clones", request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceDto>(Options, ct);
        return workspace ?? throw new InvalidOperationException("The server did not return the cloned workspace.");
    }

    public async Task StartAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/start", null, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    public async Task StopAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.PostAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/stop", null, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    public async Task DeleteAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}", ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));
    }

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("/api/workspaces/tutorial/cleanup", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/overview", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceOverviewDto>(Options, ct);
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
            return body;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}
