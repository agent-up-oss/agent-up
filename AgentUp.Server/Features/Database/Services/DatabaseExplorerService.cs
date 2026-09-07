using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using AgentUp.Server.Features.Database.Models;
using AgentUp.Server.Features.Database.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;
using Npgsql;

namespace AgentUp.Server.Features.Database.Services;

public sealed class DatabaseExplorerService
{
    private readonly WorkspaceQueryController _workspaces;
    private readonly DatabaseConnectionSettingsProvider _connectionSettings;
    private readonly DatabaseAdapterRegistry _adapters;

    public DatabaseExplorerService(
        WorkspaceQueryController workspaces,
        DatabaseConnectionSettingsProvider connectionSettings,
        DatabaseAdapterRegistry adapters)
    {
        _workspaces = workspaces;
        _connectionSettings = connectionSettings;
        _adapters = adapters;
    }

    public Task<DatabaseExplorerResult<DatabaseNamesDto>> ListDatabasesAsync(
        string workspaceId,
        string applicationName,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            workspaceId,
            applicationName,
            (settings, adapter, token) => adapter.ListDatabasesAsync(settings, token),
            databases => new DatabaseNamesDto(databases),
            cancellationToken);

    public Task<DatabaseExplorerResult<DatabaseTablesDto>> ListTablesAsync(
        string workspaceId,
        string applicationName,
        string database,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(database))
            return Task.FromResult(DatabaseExplorerResult<DatabaseTablesDto>.BadRequest("Database name is required."));

        return ExecuteAsync(
            workspaceId,
            applicationName,
            (settings, adapter, token) => adapter.ListTablesAsync(settings, database, token),
            tables => new DatabaseTablesDto(tables),
            cancellationToken);
    }

    public async Task<DatabaseExplorerResult<DatabaseQueryResultDto>> ExecuteQueryAsync(
        string workspaceId,
        string applicationName,
        DatabaseQueryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Database))
            return DatabaseExplorerResult<DatabaseQueryResultDto>.BadRequest("Database name is required.");
        if (string.IsNullOrWhiteSpace(request.Sql))
            return DatabaseExplorerResult<DatabaseQueryResultDto>.BadRequest("SQL is required.");

        return await ExecuteAsync(
            workspaceId,
            applicationName,
            (settings, adapter, token) => adapter.ExecuteQueryAsync(settings, request.Database, request.Sql, token),
            result => result,
            cancellationToken);
    }

    private async Task<DatabaseExplorerResult<TDto>> ExecuteAsync<TPayload, TDto>(
        string workspaceId,
        string applicationName,
        Func<DatabaseConnectionSettings, IDatabaseAdapter, CancellationToken, Task<TPayload>> execute,
        Func<TPayload, TDto> map,
        CancellationToken cancellationToken)
    {
        var context = ResolveContext(workspaceId, applicationName);
        if (context is null)
            return DatabaseExplorerResult<TDto>.NotFound();

        try
        {
            var adapter = _adapters.Resolve(context.Settings.Engine);
            var payload = await execute(context.Settings, adapter, cancellationToken);
            return DatabaseExplorerResult<TDto>.Success(map(payload));
        }
        catch (InvalidOperationException ex)
        {
            return DatabaseExplorerResult<TDto>.BadRequest(ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return DatabaseExplorerResult<TDto>.BadRequest(ex.Message);
        }
    }

    private DatabaseContext? ResolveContext(string workspaceId, string applicationName)
    {
        var workspace = _workspaces.GetById(workspaceId);
        var app = workspace?.Applications.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, applicationName, StringComparison.Ordinal));
        if (workspace is null || app is null || !app.Database)
            return null;

        var settings = _connectionSettings.Resolve(workspace, app);
        return new DatabaseContext(workspace, app, settings);
    }
}
