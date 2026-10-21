using System.Text.Json;

namespace AgentUp.Desktop.Features.Console.Providers;

public sealed class ConsoleApiClient(HttpClient http)
{
    public async Task<List<string>> GetOutputAsync(
        string workspaceId, string appName, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{workspaceId}/applications/{appName}/output", ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return Parse(document.RootElement);
    }

    internal static List<string> Parse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array)
            return [];

        var lines = new List<string>();
        foreach (var item in payload.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                lines.Add(item.GetString() ?? string.Empty);
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("text", out var text))
            {
                lines.Add(text.GetString() ?? string.Empty);
            }
        }

        return lines;
    }
}
