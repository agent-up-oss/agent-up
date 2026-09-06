using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.DTOs;

namespace AgentUp.Server.Features.Database.Interfaces;

public interface IDatabaseAdapter
{
    bool CanHandle(ApplicationInstance application);
    Task<DatabaseCatalogDto> GetCatalogAsync(ApplicationInstance application, CancellationToken cancellationToken);
    Task<DatabaseQueryResultDto> ExecuteAsync(ApplicationInstance application, DatabaseQueryRequest request, CancellationToken cancellationToken);
}
