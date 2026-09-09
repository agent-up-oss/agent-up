using System.Net.Http.Json;
using AgentUp.Desktop.Features.Validation.DTOs;
namespace AgentUp.Desktop.Features.Validation.Providers;
public sealed class ValidationFlowApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<ValidationFlowDto>> ListAsync(string workspaceId, string application, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ValidationFlowDto>>($"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows?application={Uri.EscapeDataString(application)}", ct) ?? [];
    public async Task RunAsync(string workspaceId, string id, CancellationToken ct = default)
    { using var response = await http.PostAsync($"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}/run", null, ct); response.EnsureSuccessStatusCode(); }
    public Uri ExportUri(string workspaceId, string id) => new(http.BaseAddress!, $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}/playwright");
}
