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
            Assert.That(login.ErrorMessage, Is.Null);
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
    public void Dismiss_HidesLoginAndClearsConnectionRetry()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowConnectionFailure("Could not reach the server.");

        login.Dismiss();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.IsConnectionRetry, Is.False);
            Assert.That(login.ErrorMessage, Is.Null);
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

    [Test]
    public async Task ConnectCommand_SavesServerAndHidesLoginWhenAuthenticationIsDisabled()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.Show();
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.ErrorMessage, Is.Null);
            Assert.That(login.CurrentServerUrl, Is.EqualTo("http://127.0.0.1:5100"));
            Assert.That(store.Load().Servers, Has.Count.EqualTo(1));
            Assert.That(store.Load().Servers[0].Url, Is.EqualTo("http://127.0.0.1:5100"));
        });
    }

    [Test]
    public async Task ConnectCommand_UsesSavedCredentialWithoutAskingForPassword()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var store = new InMemoryServerConnectionStore();
        var connections = new ServerConnectionService(store, http.Client);
        connections.Save("http://127.0.0.1:5000", "saved-token");
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.ShowSwitcher();

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.ErrorMessage, Is.Null);
            Assert.That(http.Client.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("saved-token"));
        });
    }

    [Test]
    public async Task ConnectingToAnotherSavedServer_EmitsServerSwitched()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.Show();
        login.ServerUrl = "http://127.0.0.1:5000";
        await login.ConnectCommand.Execute().FirstAsync();
        string? switched = null;
        using var subscription = login.ServerSwitched.Subscribe(url => switched = url);

        login.ShowSwitcher();
        login.ServerUrl = "http://127.0.0.1:5100";
        await login.ConnectCommand.Execute().FirstAsync();

        Assert.That(switched, Is.EqualTo("http://127.0.0.1:5100"));
    }

    [Test]
    public void GoBackCommand_HidesSwitcherWithoutCancellingStartupSignIn()
    {
        using var http = new DisposableTestHttpClient(_ =>
            Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();
        login.RememberConnected();
        login.ShowSwitcher();

        login.GoBackCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.IsSwitcher, Is.False);
        });
    }

    private static AuthenticationController CreateController(DisposableTestHttpClient http)
        => AuthenticationTestController.Create(http);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
