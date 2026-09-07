using System.Net;
using System.Net.Http.Json;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;
using AgentUp.Desktop.Features.Database.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Database.Unit;

[TestFixture]
public class DatabaseViewModelTests
{
    [Test]
    public async Task SelectedTable_SetsDefaultSelectQuery()
    {
        var handler = new FakeDatabaseHandler();
        var client = new DatabaseApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
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
        var handler = new FakeDatabaseHandler(queryError: "42P01: relation \"missing\" does not exist");
        var client = new DatabaseApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });
        var vm = new DatabaseViewModel(new DatabaseController(new DatabaseExplorerService(client)));

        await vm.LoadAsync("ws-1", "Database");
        vm.SqlQuery = "SELECT * FROM missing";
        await vm.RunQueryCommand.Execute().FirstAsync();

        Assert.That(vm.ErrorMessage, Is.EqualTo("42P01: relation \"missing\" does not exist"));
        Assert.That(vm.Rows, Is.Empty);
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
                return Json(new { tables = new[] { "products" } });

            if (request.RequestUri.AbsolutePath.EndsWith("/database/query", StringComparison.Ordinal))
            {
                if (_queryError is not null)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = JsonContent.Create(new { detail = _queryError })
                    });
                }

                return Json(new { columns = new[] { "id" }, rows = new[] { new[] { "1" } } });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json(object payload)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            });
    }
}
