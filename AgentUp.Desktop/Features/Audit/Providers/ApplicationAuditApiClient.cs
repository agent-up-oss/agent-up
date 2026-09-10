using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.Providers;

public sealed class ApplicationAuditApiClient(HttpClient http)
{
    public HttpClient Http => http;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<ApplicationAuditPageDto> GetPageAsync(
        string workspaceId,
        string application,
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> streams,
        DateTimeOffset? before,
        string? beforeEventId,
        int limit,
        CancellationToken cancellationToken)
    {
        var kindQuery = kinds.Count == 0
            ? string.Empty
            : string.Concat(kinds.Select(kind => $"&kinds={Uri.EscapeDataString(kind)}"));
        var streamQuery = streams.Count == 0
            ? string.Empty
            : string.Concat(streams.Select(stream => $"&streams={Uri.EscapeDataString(stream)}"));
        var cursor = before is null
            ? string.Empty
            : $"&before={Uri.EscapeDataString(before.Value.ToString("O"))}&beforeEventId={Uri.EscapeDataString(beforeEventId ?? string.Empty)}";
        var path = $"api/audit/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(application)}?limit={limit}{kindQuery}{streamQuery}{cursor}";
        return await http.GetFromJsonAsync<ApplicationAuditPageDto>(path, Options, cancellationToken)
            ?? new ApplicationAuditPageDto([], null, null);
    }
}
