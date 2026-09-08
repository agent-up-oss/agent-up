using System.Net;
using System.Reactive.Linq;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class LoginViewModelTests
{
    [Test]
    public async Task SignInCommand_ReturnsTokenAndHidesLogin()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}"));
        var login = new LoginViewModel(CreateController(http));
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
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();

        login.Cancel();

        Assert.That(login.IsVisible, Is.False);
        Assert.That(await login.WaitForSignInAsync(), Is.Null);
    }

    [Test]
    public async Task SignInCommand_SurfacesInvalidPassword()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.Unauthorized, "{}"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();
        login.Password = "wrong";

        await login.SignInCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.ErrorMessage, Is.EqualTo("The admin password is incorrect."));
        });
    }

    [Test]
    public async Task RetryConnectionCommand_CompletesConnectionRetry()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowConnectionFailure("Could not reach the server.");

        login.RetryConnectionCommand.Execute().Subscribe();

        Assert.That(await login.WaitForConnectionRetryAsync(), Is.True);
    }

    private static AuthenticationController CreateController(DisposableTestHttpClient http) =>
        new(new AuthenticationService(new AuthenticationApiClient(http.Client)));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
