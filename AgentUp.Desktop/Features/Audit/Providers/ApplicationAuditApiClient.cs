using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.Providers;

public sealed class ApplicationAuditApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<ApplicationAuditPageDto> GetPageAsync(
        string workspaceId,
        string application,
        DateTimeOffset? before,
        string? beforeEventId,
        int limit,
        CancellationToken cancellationToken)
    {
        var cursor = before is null
            ? string.Empty
            : $"&before={Uri.EscapeDataString(before.Value.ToString("O"))}&beforeEventId={Uri.EscapeDataString(beforeEventId ?? string.Empty)}";
        var path = $"api/audit/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(application)}?limit={limit}{cursor}";
        return await http.GetFromJsonAsync<ApplicationAuditPageDto>(path, Options, cancellationToken)
            ?? new ApplicationAuditPageDto([], null, null);
    }
}
