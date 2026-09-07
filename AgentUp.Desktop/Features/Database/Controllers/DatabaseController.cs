using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Services;

namespace AgentUp.Desktop.Features.Database.Controllers;

public sealed class DatabaseController(DatabaseExplorerService service)
{
    public Task<DatabaseNamesDto> ListDatabasesAsync(
        string workspaceId,
        string appName,
        CancellationToken ct = default)
        => service.ListDatabasesAsync(workspaceId, appName, ct);

    public Task<DatabaseTablesDto> ListTablesAsync(
        string workspaceId,
        string appName,
        string database,
        CancellationToken ct = default)
        => service.ListTablesAsync(workspaceId, appName, database, ct);

    public Task<DatabaseQueryResultDto> ExecuteQueryAsync(
        string workspaceId,
        string appName,
        string database,
        string sql,
        CancellationToken ct = default)
        => service.ExecuteQueryAsync(workspaceId, appName, database, sql, ct);
}
