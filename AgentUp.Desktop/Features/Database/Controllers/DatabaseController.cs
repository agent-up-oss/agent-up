using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Services;

namespace AgentUp.Desktop.Features.Database.Controllers;

public sealed class DatabaseController(DatabaseService service)
{
    public Task<DatabaseCatalogDto> GetCatalogAsync(string workspaceId, string applicationName, CancellationToken cancellationToken) => service.GetCatalogAsync(workspaceId, applicationName, cancellationToken);
    public Task<DatabaseQueryResultDto> ExecuteAsync(string workspaceId, string applicationName, string database, string sql, CancellationToken cancellationToken) => service.ExecuteAsync(workspaceId, applicationName, database, sql, cancellationToken);
}
