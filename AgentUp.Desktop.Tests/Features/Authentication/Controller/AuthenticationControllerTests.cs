using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Controller;

[TestFixture]
public sealed class AuthenticationControllerTests
{
    [Test]
    public async Task IsRequiredAsync_DelegatesToService()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var controller = CreateController(http);

        Assert.That(await controller.IsRequiredAsync(), Is.True);
    }

    [Test]
    public async Task LoginAsync_DelegatesToService()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}"));
        var controller = CreateController(http);

        Assert.That(await controller.LoginAsync("secret"), Is.EqualTo("token-1"));
    }

    private static AuthenticationController CreateController(DisposableTestHttpClient http) =>
        new(new AuthenticationService(new AuthenticationApiClient(http.Client)));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
