using System.Net;
using System.Reactive.Linq;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;
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
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}"));
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
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();

        login.Cancel();

        Assert.That(login.IsVisible, Is.False);
        Assert.That(await login.WaitForSignInAsync(), Is.Null);
    }

    [Test]
    public async Task SignInCommand_SurfacesInvalidPassword()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.Unauthorized, "{}"));
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
    public void Dismiss_HidesLoginAndClearsError()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowConnectionFailure("Could not reach the server.");

        login.Dismiss();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public void ShowConnectionFailure_KeepsTheServerListAndDoesNotAskForAPassword()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));

        login.ShowConnectionFailure("Could not reach the server.");

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.ErrorMessage, Is.EqualTo("Could not reach the server."));
            Assert.That(login.SavedServers[0].IsFake, Is.True);
            Assert.That(login.Subtitle, Does.Contain("Choose a saved server"));
        });
    }

    [Test]
    public async Task ConnectCommand_SavesServerAndHidesLoginWhenAuthenticationIsDisabled()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
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
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var store = new InMemoryServerConnectionStore();
        var connections = FakeServerTestComposition.Connections(store, http.Client);
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
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
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
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
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

    [Test]
    public void GoBackCommand_DoesNothingWhenTheSwitcherIsTheOnlyPrompt()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();

        login.GoBackCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.IsSwitcher, Is.True);
            Assert.That(login.CanGoBack, Is.False);
            Assert.That(login.Title, Is.EqualTo("Switch server"));
        });
    }

    [Test]
    public void ShowPicker_DoesNotApplyAStoredSession()
    {
        using var savedHttp = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        AuthenticationTestController.Create(savedHttp, store)
            .SaveServer("https://agent-up.example.com", "remote-token");

        using var launchHttp = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(AuthenticationTestController.Create(launchHttp, store));

        login.ShowPicker();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.CurrentServerUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(
                login.SavedServers.Any(server => server.Url == "https://agent-up.example.com"),
                Is.True);
        });
    }

    [Test]
    public void ShowPicker_ListsDemoWithoutAskingForAPassword()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));

        login.ShowPicker();

        Assert.Multiple(() =>
        {
            Assert.That(login.Title, Is.EqualTo("Agent-Up Server"));
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.SavedServers[0].IsFake, Is.True);
            Assert.That(login.Subtitle, Does.Contain("Choose a saved server"));
        });
    }

    [Test]
    public void Show_UsesPasswordPromptCopy()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));

        login.Show();

        Assert.Multiple(() =>
        {
            Assert.That(login.Title, Is.EqualTo("Agent-Up Server"));
            Assert.That(login.Subtitle, Is.EqualTo("Enter the administrator password to continue."));
            Assert.That(login.NeedsPassword, Is.True);
        });
    }

    [Test]
    public void Cancel_DoesNothingWhenLoginIsHidden()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));

        login.Cancel();

        Assert.That(login.IsVisible, Is.False);
    }

    [Test]
    public async Task WaitForSignInAsync_ReturnsNullWhenNoPromptWasShown()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));

        Assert.That(await login.WaitForSignInAsync(), Is.Null);
    }

    [Test]
    public async Task ShowExpiredAsync_RestoresTheSessionAfterASuccessfulPassword()
    {
        using var http = new DisposableTestHttpClient(request =>
            request.Method == HttpMethod.Post
                ? Json(HttpStatusCode.OK, "{\"authenticationRequired\":true,\"accessToken\":\"token-1\"}")
                : AuthJson(request, HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        await login.ShowExpiredAsync();
        login.Password = "secret";
        var restored = false;
        using var subscription = login.SessionRestored.Subscribe(_ => restored = true);

        await login.SignInCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.True);
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.AccessToken, Is.EqualTo("token-1"));
        });
    }

    [Test]
    public async Task ConnectCommand_AsksForPasswordWhenTheServerRequiresSignIn()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.NeedsPassword, Is.True);
            Assert.That(login.Subtitle, Is.EqualTo("Enter the administrator password to continue."));
        });
    }

    [Test]
    public async Task ConnectCommand_SurfacesUnreachableServers()
    {
        using var http = new DisposableTestHttpClient(_ =>
            throw new HttpRequestException("connection refused"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.That(login.ErrorMessage, Is.EqualTo("Could not reach the server: connection refused"));
    }

    [Test]
    public async Task ConnectCommand_SurfacesInvalidServerUrls()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();
        login.ServerUrl = "not-a-url";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.That(login.ErrorMessage, Is.EqualTo("AGENTUP_SERVER_URL must be an absolute http or https URL."));
    }

    [Test]
    public async Task SignInCommand_SurfacesUnreachableServers()
    {
        using var http = new DisposableTestHttpClient(_ =>
            throw new HttpRequestException("connection refused"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();
        login.Password = "secret";

        await login.SignInCommand.Execute().FirstAsync();

        Assert.That(login.ErrorMessage, Is.EqualTo("Could not reach the server: connection refused"));
    }

    [Test]
    public async Task SignInCommand_SurfacesMissingAccessTokens()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.Show();
        login.Password = "secret";

        await login.SignInCommand.Execute().FirstAsync();

        Assert.That(login.ErrorMessage, Is.EqualTo("The server did not return an access token."));
    }

    [Test]
    public async Task SelectSavedCommand_ConnectsTheMatchingServer()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var connections = FakeServerTestComposition.Connections(store, http.Client);
        var saved = connections.Save("http://127.0.0.1:5100", null);
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.ShowSwitcher();

        await login.SelectSavedCommand.Execute(saved.Id).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.CurrentServerUrl, Is.EqualTo("http://127.0.0.1:5100"));
        });
    }

    [Test]
    public async Task SelectSavedCommand_IgnoresUnknownServers()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();

        await login.SelectSavedCommand.Execute("missing").FirstAsync();

        Assert.That(login.IsVisible, Is.True);
    }

    [Test]
    public void RemoveSavedCommand_ResetsTheUrlWhenTheLastServerIsRemoved()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var connections = FakeServerTestComposition.Connections(store, http.Client);
        var saved = connections.Save("http://127.0.0.1:5100", "token-1");
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.ShowSwitcher();
        login.ServerUrl = "https://agent-up.example.com";

        login.RemoveSavedCommand.Execute(saved.Id).Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(login.SavedServers.All(server => server.IsFake), Is.True);
            Assert.That(login.ServerUrl, Is.EqualTo(login.CurrentServerUrl));
        });
    }

    [Test]
    public void RemoveSavedCommand_KeepsRemainingServers()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var store = new InMemoryServerConnectionStore();
        var connections = FakeServerTestComposition.Connections(store, http.Client);
        var first = connections.Save("http://127.0.0.1:5000", "first");
        var second = connections.Save("http://127.0.0.1:5100", "second");
        var login = new LoginViewModel(AuthenticationTestController.Create(http, store));
        login.ShowSwitcher();

        login.RemoveSavedCommand.Execute(first.Id).Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(login.SavedServers.Count(server => !server.IsFake), Is.EqualTo(1));
            Assert.That(login.SavedServers.Single(server => !server.IsFake).Id, Is.EqualTo(second.Id));
        });
    }

    [Test]
    public async Task ConnectCommand_ConnectsToTheBuiltInFakeServerWithoutAPassword()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = FakeServerTestComposition.Client(backend);
        var store = new InMemoryServerConnectionStore();
        var login = new LoginViewModel(new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            FakeServerTestComposition.Connections(store, http, backend)));
        login.ShowSwitcher();
        login.ServerUrl = "http://127.0.0.1:9";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.CurrentServerUrl, Is.EqualTo("http://127.0.0.1:9"));
            Assert.That(login.SavedServers[0].IsFake, Is.True);
            Assert.That(login.SavedServers[0].IsActive, Is.True);
            Assert.That(login.Surfaces, Is.EqualTo(ClientSurfaceAvailability.Demo));
        });
    }

    [Test]
    public void RemoveSavedCommand_LeavesTheBuiltInFakeServer()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request,HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowSwitcher();

        login.RemoveSavedCommand.Execute("fake").Subscribe();

        Assert.That(login.SavedServers[0].IsFake, Is.True);
    }

    [Test]
    public async Task SelectSavedCommand_ConnectsDemoFromTheLaunchPicker()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = FakeServerTestComposition.Client(backend);
        var store = new InMemoryServerConnectionStore();
        var login = new LoginViewModel(new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            FakeServerTestComposition.Connections(store, http, backend)));
        login.ShowPicker();

        await login.SelectSavedCommand.Execute("fake").FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.CurrentServerUrl, Is.EqualTo("http://127.0.0.1:9"));
            Assert.That(login.Surfaces, Is.EqualTo(ClientSurfaceAvailability.Demo));
        });
    }

    [Test]
    public async Task ConnectCommand_ChoosesSignInFromAuthenticationMode()
    {
        using var http = new DisposableTestHttpClient(request =>
            HttpTestResponses.IsConnectionRequest(request)
                ? Json(HttpStatusCode.OK, ConnectionDocument("browserSso"))
                : Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowPicker();
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.NeedsBrowserSso, Is.True);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.IsVisible, Is.True);
        });
    }

    [Test]
    public async Task ConnectCommand_ErrorsOnUnknownAuthenticationMode()
    {
        using var http = new DisposableTestHttpClient(request =>
            HttpTestResponses.IsConnectionRequest(request)
                ? Json(HttpStatusCode.OK, ConnectionDocument("magicLink"))
                : Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowPicker();
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.That(login.ErrorMessage, Does.Contain("unknown authentication.mode 'magicLink'"));
    }

    [Test]
    public async Task ShowExpiredAsync_ChoosesSignInFromAuthenticationMode()
    {
        using var http = new DisposableTestHttpClient(request =>
            HttpTestResponses.IsConnectionRequest(request)
                ? Json(HttpStatusCode.OK, ConnectionDocument("externalBearer"))
                : Json(HttpStatusCode.OK, "{\"authenticationRequired\":true}"));
        var login = new LoginViewModel(CreateController(http));
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ShowExpiredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.NeedsIssuedCredential, Is.True);
            Assert.That(login.NeedsPassword, Is.False);
            Assert.That(login.ErrorMessage, Is.EqualTo("This saved sign-in is no longer valid."));
        });
    }

    [Test]
    public async Task ShowExpiredAsync_completesWhenAuthenticationIsDisabled()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request, HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(CreateController(http));
        login.ServerUrl = "http://127.0.0.1:5100";
        var restored = false;
        using var subscription = login.SessionRestored.Subscribe(_ => restored = true);

        await login.ShowExpiredAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.False);
            Assert.That(restored, Is.True);
            Assert.That(login.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task ConnectCommand_DoesNotTreatAMalformedConnectionDocumentAsLegacy()
    {
        using var http = new DisposableTestHttpClient(request =>
            HttpTestResponses.IsConnectionRequest(request)
                ? Json(HttpStatusCode.OK, "{\"kind\":\"selfHosted\"}")
                : Json(HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(CreateController(http));
        login.ShowPicker();
        login.ServerUrl = "http://127.0.0.1:5100";

        await login.ConnectCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(login.IsVisible, Is.True);
            Assert.That(login.ErrorMessage, Does.Contain("did not return a connection apiVersion"));
        });
    }

    [Test]
    public void ShowPicker_ListsTheRecommendedServer()
    {
        using var http = new DisposableTestHttpClient(request =>
            AuthJson(request, HttpStatusCode.OK, "{\"authenticationRequired\":false}"));
        var login = new LoginViewModel(AuthenticationTestController.Create(
            http,
            recommended: new RecommendedServer("recommended", "http://127.0.0.1:5288", "Agent-Up Cloud")));

        login.ShowPicker();

        Assert.Multiple(() =>
        {
            Assert.That(login.SavedServers.Any(server => server.IsRecommended), Is.True);
            Assert.That(login.SavedServers.Single(server => server.IsRecommended).CanRemove, Is.False);
        });
    }

    private static AuthenticationController CreateController(DisposableTestHttpClient http)
        => AuthenticationTestController.Create(http);

    private static HttpResponseMessage AuthJson(HttpRequestMessage request, HttpStatusCode status, string json)
        => HttpTestResponses.LegacyOrJson(request, status, json);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string ConnectionDocument(string mode = "localAdministrator") =>
        "{\"apiVersion\":\"1\",\"connectionId\":\"local\",\"kind\":\"selfHosted\",\"displayName\":\"Agent-Up\","
        + "\"authentication\":{\"mode\":\"" + mode + "\",\"prompt\":\"Enter the administrator password to continue.\",\"identifierRequired\":false},"
        + "\"workspacePresentation\":\"serverScoped\"}";
}
