using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using Npgsql;

namespace AgentUp.Server.Features.Database.Providers;

public sealed class PostgresDatabaseAdapter : IDatabaseAdapter
{
    public bool CanHandle(ApplicationInstance application)
        => application.Database && application.AllocatedPorts.Any(port => port.DefaultPort == 5432);

    public async Task<DatabaseCatalogDto> GetCatalogAsync(ApplicationInstance application, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(application, ResolveEnvironment(application, "POSTGRES_DB") ?? "postgres"));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(cancellationToken)) names.Add(reader.GetString(0));

        var databases = new List<DatabaseDto>();
        foreach (var name in names)
            databases.Add(new DatabaseDto(name, await GetTablesAsync(application, name, cancellationToken)));
        return new DatabaseCatalogDto(databases);
    }

    public async Task<DatabaseQueryResultDto> ExecuteAsync(ApplicationInstance application, DatabaseQueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Database) || string.IsNullOrWhiteSpace(request.Sql))
            throw new ArgumentException("Database and SQL are required.");

        await using var connection = new NpgsqlConnection(ConnectionString(application, request.Database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(request.Sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<IReadOnlyList<string?>>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)).ToList());
        return new DatabaseQueryResultDto(columns, rows, reader.RecordsAffected);
    }

    private static async Task<IReadOnlyList<DatabaseTableDto>> GetTablesAsync(ApplicationInstance application, string database, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString(application, database));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new List<DatabaseTableDto>();
        while (await reader.ReadAsync(cancellationToken)) tables.Add(new DatabaseTableDto(reader.GetString(0), reader.GetString(1)));
        return tables;
    }

    private static string ConnectionString(ApplicationInstance application, string database)
    {
        var port = application.AllocatedPorts.First(mapping => mapping.DefaultPort == 5432).AllocatedPort;
        return new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = port, Database = database,
            Username = ResolveEnvironment(application, "POSTGRES_USER") ?? "postgres",
            Password = ResolveEnvironment(application, "POSTGRES_PASSWORD") ?? string.Empty,
            Timeout = 5, CommandTimeout = 30
        }.ConnectionString;
    }

    private static string? ResolveEnvironment(ApplicationInstance application, string name)
        => application.Environment?.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
