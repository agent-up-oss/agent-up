using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;

namespace AgentUp.Desktop.Tests.Features.Database.Controller;

[TestFixture]
public sealed class DatabaseControllerTests
{
    [Test]
    public async Task GetCatalog_returnsApiCatalog()
    {
        var http = new HttpClient(new JsonHandler("{\"databases\":[{\"name\":\"postgres\",\"tables\":[]}]}")) { BaseAddress = new Uri("http://localhost") };
        var controller = new DatabaseController(new DatabaseService(new DatabaseApiClient(http)));
        var catalog = await controller.GetCatalogAsync("ws", "db", CancellationToken.None);
        Assert.That(catalog.Databases.Single().Name, Is.EqualTo("postgres"));
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
