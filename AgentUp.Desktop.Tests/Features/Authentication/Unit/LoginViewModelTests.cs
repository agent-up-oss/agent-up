using System.Net;
using System.Reactive.Linq;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class LoginViewModelTests
{
    [Test]
    public async Task SignInCommand_ReturnsTokenAndHidesLogin()
    {
        var authentication = CreateController(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}"));
        var login = new LoginViewModel(authentication);
        login.Show();
        login.Password = "secret";

        await login.SignInCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.AccessToken, Is.EqualTo("token-1"));
            Assert.That(login.IsVisible, Is.False);
        });
    }

    [Test]
    public async Task Cancel_CompletesWaitForSignInWithNull()
    {
        var login = new LoginViewModel(CreateController(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}")));
        login.Show();

        login.Cancel();

        Assert.That(login.IsVisible, Is.False);
        Assert.That(await login.WaitForSignInAsync(), Is.Null);
    }

    [Test]
    public async Task SignInCommand_SurfacesInvalidPassword()
    {
        var login = new LoginViewModel(CreateController(_ =>
            Json(HttpStatusCode.Unauthorized, "{}")));
        login.Show();
        login.Password = "wrong";

        await login.SignInCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.ErrorMessage, Is.EqualTo("The admin password is incorrect."));
        });
    }

    private static AuthenticationController CreateController(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new AuthenticationService(new AuthenticationApiClient(CreateClient(response))));

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new StubHandler(response)) { BaseAddress = new Uri("http://server") };

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
