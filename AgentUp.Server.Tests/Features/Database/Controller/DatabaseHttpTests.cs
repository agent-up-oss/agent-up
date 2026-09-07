using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.Controllers;
using AgentUp.Server.Features.Database.DTOs;
using AgentUp.Server.Features.Database.Interfaces;
using AgentUp.Server.Features.Database.Models;
using AgentUp.Server.Features.Database.Providers;
using AgentUp.Server.Features.Database.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Database.Controller;

[TestFixture]
public class DatabaseHttpTests
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private string _workspaceId = null!;

    [SetUp]
    public async Task SetUp()
    {
        var port = FindFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(DatabaseHttpController).Assembly)
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService([]));
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<IDatabaseAdapter, FakeDatabaseAdapter>();
        builder.Services.AddSingleton<DatabaseAdapterRegistry>();
        builder.Services.AddSingleton<DatabaseConnectionSettingsProvider>();
        builder.Services.AddSingleton<DatabaseExplorerService>();
        builder.Services.AddSingleton<DatabasePresentationService>();

        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        var registry = _app.Services.GetRequiredService<WorkspaceRegistry>();
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Demo", "/repo", "/repo", "main", "abc")
        {
            Services =
            [
                new DockerServiceDefinition(
                    "Database",
                    "postgres:16",
                    [new PortDeclaration("POSTGRES_PORT", 5432, "tcp")],
                    new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "secret" },
                    Database: true)
            ]
        });
        _workspaceId = workspace.Id;
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task ListDatabases_ReturnsAdapterCatalog()
    {
        var response = await _client.GetFromJsonAsync<DatabaseNamesResponse>(
            $"/api/workspaces/{_workspaceId}/applications/Database/database/databases");

        Assert.That(response!.Databases, Is.EqualTo(new[] { "inventory", "postgres" }));
    }

    [Test]
    public async Task ExecuteQuery_ReturnsRows()
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/workspaces/{_workspaceId}/applications/Database/database/query",
            new { database = "inventory", sql = "SELECT * FROM products LIMIT 1" });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DatabaseQueryResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(result!.Columns, Is.EqualTo(new[] { "id", "name" }));
            Assert.That(result.Rows, Has.Count.EqualTo(1));
            Assert.That(result.Rows[0], Is.EqualTo(new[] { "1", "Widget" }));
        });
    }

    private static int FindFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record DatabaseNamesResponse(IReadOnlyList<string> Databases);
    private sealed record DatabaseQueryResponse(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows);

    private sealed class FakeDatabaseAdapter : IDatabaseAdapter
    {
        public string Engine => "postgres";

        public Task<IReadOnlyList<string>> ListDatabasesAsync(
            DatabaseConnectionSettings settings,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["inventory", "postgres"]);

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
            => Task.FromResult(new DatabaseQueryResultDto(["id", "name"], [["1", "Widget"]]));
    }
}
