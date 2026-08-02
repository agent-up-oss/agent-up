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

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("/api/workspaces/tutorial/cleanup", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
