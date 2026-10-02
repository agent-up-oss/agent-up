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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.LoginAsync("wrong"));
        Assert.That(exception!.Message, Is.EqualTo("The admin password is incorrect."));
    }

    [Test]
    public async Task IsAuthenticationRequiredAsync_ThrowsWhenThePayloadIsInvalid()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK, "null"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.IsAuthenticationRequiredAsync());
        Assert.That(exception!.Message, Is.EqualTo("Invalid authentication status response."));
    }

    [Test]
    public async Task LoginAsync_ThrowsWhenTheServerOmitsAnAccessToken()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.LoginAsync("secret"));
        Assert.That(exception!.Message, Is.EqualTo("The server did not return an access token."));
    }

    [Test]
    public async Task ResolveConnectionAsync_parsesTheConnectionDocument()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK,
            "{\"apiVersion\":\"1\",\"connectionId\":\"local\",\"kind\":\"selfHosted\",\"displayName\":\"Agent-Up\","
            + "\"authentication\":{\"mode\":\"disabled\",\"prompt\":\"Authentication is not required for this Server.\",\"identifierRequired\":false},"
            + "\"workspacePresentation\":\"serverScoped\"}"));
        var client = new AuthenticationApiClient(http.Client);

        var source = await client.ResolveConnectionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.AuthMode, Is.EqualTo("disabled"));
            Assert.That(source.IsLegacy, Is.False);
        });
    }

    [Test]
    public async Task ResolveConnectionAsync_treatsAMissingDocumentAsLegacyAfterAuthStatus()
    {
        using var http = new DisposableTestHttpClient(request =>
            request.RequestUri?.AbsolutePath == "/api/connection"
                ? Json(HttpStatusCode.NotFound, "{}")
                : Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var client = new AuthenticationApiClient(http.Client);

        var source = await client.ResolveConnectionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.IsLegacy, Is.True);
            Assert.That(source.AuthMode, Is.EqualTo("localAdministrator"));
        });
    }

    [Test]
    public async Task ResolveConnectionAsync_throwsWhenTheDocumentIsNull()
    {
        using var http = new DisposableTestHttpClient(_ => Json(HttpStatusCode.OK, "null"));
        var client = new AuthenticationApiClient(http.Client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.ResolveConnectionAsync());
        Assert.That(exception!.Message, Is.EqualTo("This Server did not return a connection document."));
    }

    [Test]
    public async Task ResolveConnectionAsync_throwsWhenTheServerUrlIsNotConfigured()
    {
        using var http = new HttpClient(new NullDocumentHandler());
        var client = new AuthenticationApiClient(http);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.ResolveConnectionAsync());
        Assert.That(exception!.Message, Is.EqualTo("The Server URL is not configured."));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class NullDocumentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(Json(HttpStatusCode.OK, "null"));
    }
}
