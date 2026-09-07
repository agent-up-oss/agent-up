using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Desktop.Features.Database.DTOs;

namespace AgentUp.Desktop.Features.Database.Providers;

public sealed class DatabaseApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<DatabaseNamesDto> ListDatabasesAsync(
        string workspaceId,
        string appName,
        CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/database/databases",
            ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));

        var result = await response.Content.ReadFromJsonAsync<DatabaseNamesDto>(Options, ct);
        return result ?? new DatabaseNamesDto([]);
    }

    public async Task<DatabaseTablesDto> ListTablesAsync(
        string workspaceId,
        string appName,
        string database,
        CancellationToken ct = default)
    {
        using var response = await http.GetAsync(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/database/tables?database={Uri.EscapeDataString(database)}",
            ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));

        var result = await response.Content.ReadFromJsonAsync<DatabaseTablesDto>(Options, ct);
        return result ?? new DatabaseTablesDto([]);
    }

    public async Task<DatabaseQueryResultDto> ExecuteQueryAsync(
        string workspaceId,
        string appName,
        string database,
        string sql,
        CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/database/query",
            new { database, sql },
            Options,
            ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadProblemDetailAsync(response));

        var result = await response.Content.ReadFromJsonAsync<DatabaseQueryResultDto>(Options, ct);
        return result ?? new DatabaseQueryResultDto([], []);
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
                return $"HTTP {(int)response.StatusCode}";

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString() ?? body;
            }

            if (doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object)
            {
                var message = errors.EnumerateObject()
                    .Where(property => property.Value.ValueKind == JsonValueKind.Array
                                       && property.Value.GetArrayLength() > 0)
                    .Select(property => property.Value[0].GetString())
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
                if (message is not null)
                    return message;
            }

            return body;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return $"HTTP {(int)response.StatusCode}";
        }
    }
}
