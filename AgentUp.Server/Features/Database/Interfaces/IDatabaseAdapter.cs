using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Models;

namespace AgentUp.Server.Features.Database.Interfaces;

public interface IDatabaseAdapter
{
    string Engine { get; }

    Task<IReadOnlyList<string>> ListDatabasesAsync(
        DatabaseConnectionSettings settings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListTablesAsync(
        DatabaseConnectionSettings settings,
        string database,
        CancellationToken cancellationToken = default);

    Task<DatabaseQueryResultDto> ExecuteQueryAsync(
        DatabaseConnectionSettings settings,
        string database,
        string sql,
        CancellationToken cancellationToken = default);
}
