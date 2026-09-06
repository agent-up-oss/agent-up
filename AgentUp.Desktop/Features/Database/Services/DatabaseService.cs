using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Providers;

namespace AgentUp.Desktop.Features.Database.Services;

public sealed class DatabaseService(DatabaseApiClient client)
{
    public Task<DatabaseCatalogDto> GetCatalogAsync(string workspaceId, string applicationName, CancellationToken cancellationToken) => client.GetCatalogAsync(workspaceId, applicationName, cancellationToken);
    public Task<DatabaseQueryResultDto> ExecuteAsync(string workspaceId, string applicationName, string database, string sql, CancellationToken cancellationToken) => client.ExecuteAsync(workspaceId, applicationName, database, sql, cancellationToken);
}
