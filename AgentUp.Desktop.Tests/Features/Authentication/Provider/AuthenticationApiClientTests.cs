using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public class AuthenticationApiClientTests
{
    [Test]
    public async Task IsAuthenticationRequiredAsync_ReadsServerStatus()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var client = new AuthenticationApiClient(http.Client);

        Assert.That(await client.IsAuthenticationRequiredAsync(), Is.False);
    }

    [Test]
    public async Task LoginAsync_ReturnsAccessToken()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK,
            "{\"authenticationRequired\":true,\"accessToken\":\"abc\"}"));
        var client = new AuthenticationApiClient(http.Client);

        Assert.That(await client.LoginAsync("secret"), Is.EqualTo("abc"));
    }

    [Test]
    public async Task LoginAsync_ThrowsForUnauthorizedPassword()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.Unauthorized, "{}"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.LoginAsync("wrong"));
        Assert.That(exception!.Message, Is.EqualTo("The admin password is incorrect."));
    }

    [Test]
    public void IsAuthenticationRequiredAsync_ThrowsWhenThePayloadIsInvalid()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK, "null"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.IsAuthenticationRequiredAsync());
        Assert.That(exception!.Message, Is.EqualTo("Invalid authentication status response."));
    }

    [Test]
    public void LoginAsync_ThrowsWhenTheServerOmitsAnAccessToken()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.LoginAsync("secret"));
        Assert.That(exception!.Message, Is.EqualTo("The server did not return an access token."));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
