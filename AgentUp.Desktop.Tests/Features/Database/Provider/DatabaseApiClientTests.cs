using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Providers;

namespace AgentUp.Desktop.Tests.Features.Database.Provider;

[TestFixture]
public class DatabaseApiClientTests
{
    [Test]
    public async Task ListDatabasesAsync_DeserializesResponse()
    {
        var handler = new FakeHandler();
        var client = new DatabaseApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        var result = await client.ListDatabasesAsync("ws-1", "Database");

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
