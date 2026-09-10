using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.Providers;

public sealed class ApplicationAuditStreamClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task StreamAsync(
        string workspaceId,
        string application,
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> streams,
        Action<ApplicationAuditEventDto> onEvent,
        CancellationToken cancellationToken)
    {
        var kindQuery = kinds.Count == 0
            ? string.Empty
            : string.Concat(kinds.Select(kind => $"&kinds={Uri.EscapeDataString(kind)}"));
        var streamQuery = streams.Count == 0
            ? string.Empty
            : string.Concat(streams.Select(stream => $"&streams={Uri.EscapeDataString(stream)}"));
        var path =
            $"api/audit/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(application)}/stream?{string.Concat(kindQuery, streamQuery).TrimStart('&')}";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                return;

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var json = line["data: ".Length..];
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                var evt = JsonSerializer.Deserialize<ApplicationAuditEventDto>(json, JsonOptions);
                if (evt is not null)
                    onEvent(evt);
            }
            catch (JsonException ex)
            {
                Trace.TraceWarning($"[ApplicationAuditStreamClient] Skipped malformed event: {ex.Message}");
            }
        }
    }
}
