using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Desktop.Features.Browser.Models;


namespace AgentUp.Desktop.Features.Browser.Providers;

internal sealed class BrowserCommandHttpClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<BrowserCommandDto?> GetPendingCommandAsync(
        IEnumerable<string> workspaceIds,
        int timeoutMs,
        CancellationToken ct)
    {
        var ids = string.Join(',', workspaceIds);
        if (string.IsNullOrEmpty(ids)) return null;

        using var response = await http.GetAsync(
            $"/api/browser/pending-command?workspaceIds={Uri.EscapeDataString(ids)}&timeoutMs={timeoutMs}",
            ct);

        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrowserCommandDto>(Options, ct);
    }

    public async Task PostCommandResultAsync(BrowserCommandResultDto result, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/api/browser/command-result", result, Options, ct);
        response.EnsureSuccessStatusCode();
    }
}
