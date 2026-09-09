using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgentUp.Desktop.Features.Validation.DTOs;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class ValidationFlowApiClient(HttpClient http)
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<ValidationFlowDto>> ListAsync(
        string workspaceId,
        string application,
        CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<ValidationFlowDto>>(
            $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows?application={Uri.EscapeDataString(application)}",
            JsonOptions,
            cancellationToken) ?? [];

    public async Task<ValidationFlowDto?> GetAsync(
        string workspaceId,
        string id,
        CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<ValidationFlowDto>(
            $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}",
            JsonOptions,
            cancellationToken);

    public Uri ExportUri(string workspaceId, string id) =>
        new(http.BaseAddress!, $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}/playwright");
}
