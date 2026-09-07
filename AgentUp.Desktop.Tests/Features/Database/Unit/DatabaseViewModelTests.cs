using System.Net;
using System.Net.Http.Json;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;
using AgentUp.Desktop.Tests.Features.Database.Support;
using AgentUp.Desktop.Features.Database.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Database.Unit;

[TestFixture]
public class DatabaseViewModelTests
{
    [Test]
    public async Task SelectedTable_SetsDefaultSelectQuery()
    {
        using var handler = new FakeDatabaseHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new DatabaseApiClient(http);
        var vm = new DatabaseViewModel(new DatabaseController(new DatabaseExplorerService(client)));

        await vm.LoadAsync("ws-1", "Database");
        vm.SelectedTable = "products";

        Assert.That(vm.SqlQuery, Is.EqualTo("SELECT * FROM \"products\" LIMIT 50"));
        Assert.That(vm.Rows, Has.Count.EqualTo(1));
        Assert.That(vm.Columns[0].Name, Is.EqualTo("id"));
    }

    [Test]
    public async Task RunQuery_SurfacesServerSqlError()
    {
        using var handler = new FakeDatabaseHandler(queryError: "42P01: relation \"missing\" does not exist");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new DatabaseApiClient(http);
        var vm = new DatabaseViewModel(new DatabaseController(new DatabaseExplorerService(client)));

        await vm.LoadAsync("ws-1", "Database");
        vm.SqlQuery = "SELECT * FROM missing";
        await vm.ExecuteRunQueryAsync();

        Assert.That(vm.ErrorMessage, Is.EqualTo("42P01: relation \"missing\" does not exist"));
        Assert.That(vm.Rows, Is.Empty);
    }

    [Test]
    public async Task RunQuery_IgnoresStaleResults_WhenClearedDuringQuery()
    {
        using var handler = new DelayedDatabaseHandler(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new DatabaseApiClient(http);
        var vm = new DatabaseViewModel(new DatabaseController(new DatabaseExplorerService(client)));

        await vm.LoadAsync("ws-1", "Database");
        await Task.Delay(50);
        vm.SqlQuery = "SELECT * FROM products";
        var slowQuery = vm.ExecuteRunQueryAsync();
        vm.Clear();
        await slowQuery;

        Assert.That(vm.Rows, Is.Empty);
        Assert.That(vm.Columns, Is.Empty);
    }

    private sealed class FakeDatabaseHandler : HttpMessageHandler
    {
        private readonly string? _queryError;

        public FakeDatabaseHandler(string? queryError = null) => _queryError = queryError;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/database/databases", StringComparison.Ordinal))
                return Json(new { databases = new[] { "inventory" } });

            if (request.RequestUri.AbsolutePath.EndsWith("/database/tables", StringComparison.Ordinal))
                return Json(new { tables = new[] { "products", "orders" } });

            if (request.RequestUri.AbsolutePath.EndsWith("/database/query", StringComparison.Ordinal))
            {
                if (_queryError is not null)
                    return Task.FromResult(HttpTestResponses.Json(HttpStatusCode.BadRequest, new { detail = _queryError }));

                return Json(new { columns = new[] { "id" }, rows = new[] { new[] { "1" } } });
            }

            return Task.FromResult(HttpTestResponses.Json(HttpStatusCode.NotFound, new { }));
        }

        private static Task<HttpResponseMessage> Json(object payload)
            => Task.FromResult(HttpTestResponses.Json(payload));
    }

    private sealed class DelayedDatabaseHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _ordersCompleted;

        public DelayedDatabaseHandler(TaskCompletionSource ordersCompleted)
            => _ordersCompleted = ordersCompleted;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/database/databases", StringComparison.Ordinal))
                return await Json(new { databases = new[] { "inventory" } });

            if (request.RequestUri.AbsolutePath.EndsWith("/database/tables", StringComparison.Ordinal))
                return await Json(new { tables = new[] { "products", "orders" } });

            if (request.RequestUri.AbsolutePath.EndsWith("/database/query", StringComparison.Ordinal))
            {
                var sql = await request.Content!.ReadAsStringAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
                if (sql.Contains("\"orders\"", StringComparison.Ordinal))
                {
                    var response = await Json(new { columns = new[] { "order_id" }, rows = new[] { new[] { "ORD-1" } } });
                    _ordersCompleted.TrySetResult();
                    return response;
                }

                var staleResponse = await Json(new { columns = new[] { "id" }, rows = new[] { new[] { "stale" } } });
                _ordersCompleted.TrySetResult();
                return staleResponse;
            }

            return HttpTestResponses.Json(HttpStatusCode.NotFound, new { });
        }

        private static Task<HttpResponseMessage> Json(object payload)
            => Task.FromResult(HttpTestResponses.Json(payload));
    }
}
