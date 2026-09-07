using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Database.Controllers;
using AgentUp.Desktop.Features.Database.DTOs;
using AgentUp.Desktop.Features.Database.Providers;
using AgentUp.Desktop.Features.Database.Services;

namespace AgentUp.Desktop.Tests.Features.Database.Provider;

[TestFixture]
public class DatabaseApiClientTests
{
    [Test]
    public async Task ListDatabasesAsync_DeserializesResponse()
    {
        using var handler = new FakeHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new DatabaseApiClient(http);

        var result = await client.ListDatabasesAsync("ws-1", "Database");

        Assert.That(result.Databases, Is.EqualTo(new[] { "inventory" }));
    }

    [Test]
    public async Task ExecuteQueryAsync_SurfacesProblemDetail()
    {
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, """{"detail":"syntax error at or near \"FROM\""}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new DatabaseApiClient(http);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.ExecuteQueryAsync("ws-1", "Database", "inventory", "SELECT FROM"));

        Assert.That(ex!.Message, Is.EqualTo("syntax error at or near \"FROM\""));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _body;

        public FakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string? body = null)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_body is not null)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DatabaseNamesDto(["inventory"]))
            });
        }
    }
}
