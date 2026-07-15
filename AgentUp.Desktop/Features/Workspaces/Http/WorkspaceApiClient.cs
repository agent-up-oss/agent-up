using System.Net.Http.Json;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Workspaces.Http;

public sealed class WorkspaceApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<WorkspaceDto>> ListAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<List<WorkspaceDto>>("/api/workspaces", Options, ct);
        return result ?? [];
    }

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken ct = default)
    {
        var response = await http.PostAsync("/api/workspaces/tutorial/cleanup", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
