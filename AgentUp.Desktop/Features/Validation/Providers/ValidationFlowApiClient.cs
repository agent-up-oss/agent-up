using System.Net;
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

    // A missing flow is an answer, not a transport failure. GetFromJsonAsync throws on the 404,
    // which hid the null that ValidationFlowReplayService.RunAsync branches on. Other statuses
    // still fail loudly.
    public async Task<ValidationFlowDto?> GetAsync(
        string workspaceId,
        string id,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ValidationFlowDto>(JsonOptions, cancellationToken);
    }

    public Uri ExportUri(string workspaceId, string id) =>
        new(http.BaseAddress!, $"api/workspaces/{Uri.EscapeDataString(workspaceId)}/validation-flows/{Uri.EscapeDataString(id)}/playwright");
}
