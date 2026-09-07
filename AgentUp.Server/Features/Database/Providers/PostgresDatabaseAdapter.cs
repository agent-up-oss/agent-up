using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using AgentUp.Server.Features.Database.Models;
using Npgsql;

namespace AgentUp.Server.Features.Database.Providers;

public sealed class PostgresDatabaseAdapter : IDatabaseAdapter
{
    public string Engine => "postgres";

    public async Task<IReadOnlyList<string>> ListDatabasesAsync(
        DatabaseConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(settings with { Database = "postgres" }, cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT datname
            FROM pg_database
            WHERE datistemplate = false
            ORDER BY datname
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var databases = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            databases.Add(reader.GetString(0));

        return databases;
    }

    public async Task<IReadOnlyList<string>> ListTablesAsync(
        DatabaseConnectionSettings settings,
        string database,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(settings with { Database = database }, cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
              AND table_type = 'BASE TABLE'
            ORDER BY table_schema, table_name
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            tables.Add(string.Equals(schema, "public", StringComparison.Ordinal)
                ? table
                : $"{schema}.{table}");
        }

        return tables;
    }

    public async Task<DatabaseQueryResultDto> ExecuteQueryAsync(
        DatabaseConnectionSettings settings,
        string database,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(settings with { Database = database }, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        var rows = new List<IReadOnlyList<string>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                values[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;

            rows.Add(values);
        }

        return new DatabaseQueryResultDto(columns, rows);
    }

    private static async Task<NpgsqlConnection> OpenAsync(
        DatabaseConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = settings.Port,
            Username = settings.Username,
            Password = settings.Password,
            Database = settings.Database,
            Timeout = 5,
            CommandTimeout = 30
        };

        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
