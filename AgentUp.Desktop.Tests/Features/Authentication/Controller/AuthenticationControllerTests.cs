using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
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

    [Test]
    public void SaveServer_PersistsNormalizedUrl()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var controller = AuthenticationTestController.Create(http, store);

        var saved = controller.SaveServer("http://127.0.0.1:5100/", "token-1");

        Assert.Multiple(() =>
        {
            Assert.That(saved.Url, Is.EqualTo("http://127.0.0.1:5100"));
            Assert.That(controller.ListSavedServers().Servers, Has.Count.EqualTo(1));
            Assert.That(controller.CurrentServerUrl(), Is.EqualTo("http://127.0.0.1:5100"));
        });
    }

    [Test]
    public void ActivateServer_SelectsTheSavedConnection()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var controller = AuthenticationTestController.Create(http, store);
        var first = controller.SaveServer("http://127.0.0.1:5000", "first");
        var second = controller.SaveServer("http://127.0.0.1:5100", "second");

        var activated = controller.ActivateServer(first.Id);

        Assert.Multiple(() =>
        {
            Assert.That(activated.Id, Is.EqualTo(first.Id));
            Assert.That(controller.CurrentServerUrl(), Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(second.Url, Is.EqualTo("http://127.0.0.1:5100"));
        });
    }

    [Test]
    public void RemoveServer_DropsTheSavedConnection()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var controller = AuthenticationTestController.Create(http, store);
        var saved = controller.SaveServer("http://127.0.0.1:5100", "token-1");

        controller.RemoveServer(saved.Id);

        Assert.That(controller.ListSavedServers().Servers, Is.Empty);
    }

    [Test]
    public void RestoreActiveServer_AppliesTheStoredSelection()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var controller = AuthenticationTestController.Create(http, store);
        controller.SaveServer("http://127.0.0.1:5100", "token-1");
        controller.PrepareServer("http://127.0.0.1:5000");

        controller.RestoreActiveServer();

        Assert.That(controller.CurrentServerUrl(), Is.EqualTo("http://127.0.0.1:5100"));
    }

    private static AuthenticationController CreateController(DisposableTestHttpClient http)
        => AuthenticationTestController.Create(http);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
