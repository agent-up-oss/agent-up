using System.Net.Http.Json;
using AgentUp.Desktop.Features.Database.DTOs;

namespace AgentUp.Desktop.Features.Database.Providers;

public sealed class DatabaseApiClient(HttpClient http)
{
    public async Task<DatabaseCatalogDto> GetCatalogAsync(string workspaceId, string applicationName, CancellationToken cancellationToken)
        => await http.GetFromJsonAsync<DatabaseCatalogDto>(Path(workspaceId, applicationName), cancellationToken) ?? new DatabaseCatalogDto([]);

    public async Task<DatabaseQueryResultDto> ExecuteAsync(string workspaceId, string applicationName, string database, string sql, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Path(workspaceId, applicationName)}/query", new { database, sql }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Database query failed: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<DatabaseQueryResultDto>(cancellationToken) ?? new DatabaseQueryResultDto([], [], 0);
    }

    private static string Path(string workspaceId, string applicationName)
        => $"/api/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(applicationName)}/database";
}
