using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.AUDebug.Features.Desktop.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Tests.Features.Desktop.Provider;

[TestFixture]
public sealed class DesktopWorkspaceApiClientTests
{
    [Test]
    public async Task StartByName_logsInAndPostsStart()
    {
        var started = false;
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new { accessToken = "tok" });
            if (request.RequestUri.AbsolutePath == "/api/workspaces" && request.Method == HttpMethod.Get)
                return Json(new[] { new { id = "ws-1", displayName = "Agent-Up", state = "stopped" } });
            if (request.RequestUri.AbsolutePath == "/api/workspaces/ws-1/start")
            {
                started = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None);

        Assert.That(started, Is.True);
    }

    [Test]
    public void MissingWorkspace_throws()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new { accessToken = "tok" });
            return Json(Array.Empty<object>());
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Nope", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("No workspace named"));
    }

    [Test]
    public void WrongPassword_throws()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "nope", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("incorrect"));
    }

    [Test]
    public void LoginFailure_throws()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "boom"
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Login failed"));
    }

    [Test]
    public void MissingAccessToken_throws()
    {
        using var handler = new StubHandler(_ => Json(new { token = "nope" }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("did not return an access token"));
    }

    [Test]
    public void StartFailure_throws()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new { accessToken = "tok" });
            if (request.RequestUri.AbsolutePath == "/api/workspaces" && request.Method == HttpMethod.Get)
                return Json(new[] { new { id = "ws-1", displayName = "Agent-Up", state = "stopped" } });
            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = "start failed" };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Failed to start"));
    }

    [Test]
    public void ListUnauthorized_throws()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new { accessToken = "tok" });
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("authentication is required"));
    }

    [Test]
    public void ListFailure_throws()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new { accessToken = "tok" });
            return new HttpResponseMessage(HttpStatusCode.BadGateway) { ReasonPhrase = "down" };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(DebugLayout.ServerUrl) };

        Assert.That(
            async () => await new DesktopWorkspaceApiClient(http).StartByNameAsync("Agent-Up", "test", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Failed to list workspaces"));
    }

    private static HttpResponseMessage Json(object payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response(request));
    }
}
