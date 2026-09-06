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
        var result = await http.GetFromJsonAsync<DatabaseNamesDto>(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/database/databases",
            Options,
            ct);
        return result ?? new DatabaseNamesDto([]);
    }

    public async Task<DatabaseTablesDto> ListTablesAsync(
        string workspaceId,
        string appName,
        string database,
        CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<DatabaseTablesDto>(
            $"/api/workspaces/{workspaceId}/applications/{Uri.EscapeDataString(appName)}/database/tables?database={Uri.EscapeDataString(database)}",
            Options,
            ct);
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
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DatabaseQueryResultDto>(Options, ct);
        return result ?? new DatabaseQueryResultDto([], []);
    }
}
