using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public class AuthenticationApiClientTests
{
    [Test]
    public async Task IsAuthenticationRequiredAsync_ReadsServerStatus()
    {
        var client = CreateClient(_ => Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));

        Assert.That(await client.IsAuthenticationRequiredAsync(), Is.False);
    }

    [Test]
    public async Task LoginAsync_ReturnsAccessToken()
    {
        var client = CreateClient(_ => Json(HttpStatusCode.OK,
            "{\"authenticationRequired\":true,\"accessToken\":\"abc\"}"));

        Assert.That(await client.LoginAsync("secret"), Is.EqualTo("abc"));
    }

    private static AuthenticationApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://server") });

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
