using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;

namespace AgentUp.Desktop.Tests.Features.Database.Controller;

[TestFixture]
public class DatabaseControllerTests
{
    [Test]
    public async Task ListDatabasesAsync_DelegatesToApiClient()
    {
        var handler = new FakeHandler();
        var controller = new DatabaseController(new DatabaseExplorerService(
            new DatabaseApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") })));

        var result = await controller.ListDatabasesAsync("ws-1", "Database");

        Assert.That(result.Databases, Is.EqualTo(new[] { "inventory" }));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DatabaseNamesDto(["inventory"]))
            });
    }
}
