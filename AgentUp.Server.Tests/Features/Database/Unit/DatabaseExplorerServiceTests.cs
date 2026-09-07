using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using AgentUp.Server.Features.Database.Models;
using AgentUp.Server.Features.Database.Providers;
using AgentUp.Server.Features.Database.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Database.Unit;

[TestFixture]
public class DatabaseExplorerServiceTests
{
    [Test]
    public async Task ListDatabasesAsync_ReturnsNull_WhenApplicationIsNotDatabaseEnabled()
    {
        var registry = await CreateRegistryAsync();
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Demo", "/repo", "/repo", "main", "abc")
        {
            Services =
            [
                new DockerServiceDefinition("Database", "postgres:16", [new PortDeclaration("POSTGRES_PORT", 5432, "tcp")])
            ]
        });

        var service = CreateService(registry);
        var result = await service.ListDatabasesAsync(workspace.Id, "Database");

        Assert.That(result.Status, Is.EqualTo(DatabaseExplorerStatus.NotFound));
    }

    private static DatabaseExplorerService CreateService(WorkspaceRegistry registry)
        => new(
            new WorkspaceQueryController(registry),
            new DatabaseConnectionSettingsProvider(),
            new DatabaseAdapterRegistry([new FakeDatabaseAdapter()]));

    private static async Task<WorkspaceRegistry> CreateRegistryAsync()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        return registry;
    }

    private sealed class FakeDatabaseAdapter : IDatabaseAdapter
    {
        public string Engine => "postgres";

        public Task<IReadOnlyList<string>> ListDatabasesAsync(
            DatabaseConnectionSettings settings,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["inventory"]);

        public Task<IReadOnlyList<string>> ListTablesAsync(
            DatabaseConnectionSettings settings,
            string database,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["products"]);

        public Task<DatabaseQueryResultDto> ExecuteQueryAsync(
            DatabaseConnectionSettings settings,
            string database,
            string sql,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DatabaseQueryResultDto(["id"], [["1"]]));
    }
}
