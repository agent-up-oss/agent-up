using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using Npgsql;

namespace AgentUp.Server.Features.Database.Services;

public sealed class DatabaseService(WorkspaceQueryController workspaces, IEnumerable<IDatabaseAdapter> adapters)
{
    public async Task<DatabaseOperationResult<DatabaseCatalogDto>> GetCatalogAsync(string workspaceId, string applicationName, CancellationToken cancellationToken)
    {
        var resolved = Resolve(workspaceId, applicationName);
        if (resolved is null) return DatabaseOperationResult<DatabaseCatalogDto>.NotFound();
        try
        {
            return DatabaseOperationResult<DatabaseCatalogDto>.Success(await resolved.Value.Adapter.GetCatalogAsync(resolved.Value.Application, cancellationToken));
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return DatabaseOperationResult<DatabaseCatalogDto>.Failed("Agent-Up could not connect to the database application.");
        }
    }

    public async Task<DatabaseOperationResult<DatabaseQueryResultDto>> ExecuteAsync(string workspaceId, string applicationName, DatabaseQueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Database) || string.IsNullOrWhiteSpace(request.Sql))
            return DatabaseOperationResult<DatabaseQueryResultDto>.Failed("Database and SQL are required.");
        var resolved = Resolve(workspaceId, applicationName);
        if (resolved is null) return DatabaseOperationResult<DatabaseQueryResultDto>.NotFound();
        try
        {
            return DatabaseOperationResult<DatabaseQueryResultDto>.Success(await resolved.Value.Adapter.ExecuteAsync(resolved.Value.Application, request, cancellationToken));
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException or ArgumentException)
        {
            return DatabaseOperationResult<DatabaseQueryResultDto>.Failed("PostgreSQL rejected the SQL query.");
        }
    }

    private (ApplicationInstance Application, IDatabaseAdapter Adapter)? Resolve(string workspaceId, string applicationName)
    {
        var application = workspaces.GetById(workspaceId)?.Applications.FirstOrDefault(app => app.Name == applicationName && app.Database);
        if (application is null) return null;
        var adapter = adapters.FirstOrDefault(candidate => candidate.CanHandle(application));
        return adapter is null ? null : (application, adapter);
    }
}
