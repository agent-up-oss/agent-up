using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;

namespace AgentUp.Desktop.Tests.Features.Authentication.Controller;

[TestFixture]
public sealed class AuthenticationControllerTests
{
    [Test]
    public async Task IsRequiredAsync_DelegatesToService()
    {
        var controller = CreateController(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));

        Assert.That(await controller.IsRequiredAsync(), Is.True);
    }

    [Test]
    public async Task LoginAsync_DelegatesToService()
    {
        var controller = CreateController(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}"));

        Assert.That(await controller.LoginAsync("secret"), Is.EqualTo("token-1"));
    }

    private static AuthenticationController CreateController(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new AuthenticationService(new AuthenticationApiClient(
            new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("http://server") })));

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
