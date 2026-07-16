using System.Net.Http.Json;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Console.Providers;

public sealed class ConsoleApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<string>> GetOutputAsync(
        string workspaceId, string appName, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<List<string>>(
            $"/api/workspaces/{workspaceId}/applications/{appName}/output", Options, ct);
        return result ?? [];
    }
}
