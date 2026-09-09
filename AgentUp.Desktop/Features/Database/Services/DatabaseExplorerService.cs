using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Providers;

namespace AgentUp.Desktop.Features.Database.Services;

public sealed class DatabaseExplorerService(DatabaseApiClient client)
{
    public Task<DatabaseNamesDto> ListDatabasesAsync(
        string workspaceId,
        string appName,
        CancellationToken ct = default)
        => client.ListDatabasesAsync(workspaceId, appName, ct);

    public Task<DatabaseTablesDto> ListTablesAsync(
        string workspaceId,
        string appName,
        string database,
        CancellationToken ct = default)
        => client.ListTablesAsync(workspaceId, appName, database, ct);

    public Task<DatabaseQueryResultDto> ExecuteQueryAsync(
        string workspaceId,
        string appName,
        string database,
        string sql,
        CancellationToken ct = default)
        => client.ExecuteQueryAsync(workspaceId, appName, database, sql, ct);
}
