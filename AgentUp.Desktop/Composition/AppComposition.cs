using System.Net.Http.Headers;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.FakeServer.Controllers;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Composition;

public static class AppComposition
{
    public static async Task InitializeDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var fakeBackend = new FakeBackendService(new FakeServerDefinitionProvider().LoadEmbedded());
        var fakeServers = new FakeServerController(fakeBackend);
        var http = CreateServerHttpClient(fakeBackend);
        var agentEventsHttp = CreateAgentEventHttpClient(fakeBackend);
        var connections = new ServerConnectionService(
            new FileServerConnectionStore(),
            http,
            fakeServers,
            agentEventsHttp);
        connections.RestoreActive();
        var authentication = new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            connections);
        var login = new LoginViewModel(authentication);
        var (window, viewModel) = CreateMainWindow(http, login, fakeServers, fakeBackend, agentEventsHttp);
        desktop.MainWindow = window;
        window.Closing += (_, _) => viewModel.Login.Cancel();
        window.Show();

        while (!await TryAuthenticateAsync(desktop, http, authentication, login))
            continue;

        if (desktop.MainWindow is MainWindow mainWindow)
            mainWindow.StartAuthenticatedServices();

        await viewModel.InitializeAsync();
    }

    private static async Task<bool> TryAuthenticateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        HttpClient http,
        AuthenticationController authentication,
        LoginViewModel login)
    {
        try
        {
            if (!await authentication.IsRequiredAsync())
            {
                authentication.SaveServer(authentication.CurrentServerUrl(), null);
                login.Dismiss();
                login.RememberConnected();
                return true;
            }

            if (http.DefaultRequestHeaders.Authorization is not null)
            {
                login.Dismiss();
                login.RememberConnected();
                return true;
            }

            login.Show();
            var token = await login.WaitForSignInAsync();
            if (token is null)
            {
                desktop.Shutdown();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(token))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            login.RememberConnected();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        {
            login.ShowConnectionFailure(ex.Message);
            if (!await login.WaitForConnectionRetryAsync())
            {
                desktop.Shutdown();
                return false;
            }

            return false;
        }
    }

    private static HttpClient CreateServerHttpClient(FakeBackendService fakeBackend)
        => ServerSessionProvider.CreateClient(
            SecureServerUrlProvider.ResolveServerUri(),
            new FakeServerMessageHandler(fakeBackend, new HttpClientHandler()));

    private static HttpClient CreateAgentEventHttpClient(FakeBackendService fakeBackend)
    {
        var client = CreateServerHttpClient(fakeBackend);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(
        HttpClient http,
        LoginViewModel login,
        FakeServerController fakeServers,
        FakeBackendService fakeBackend,
        HttpClient? agentEventsHttp = null)
    {
        var viewModel = MainViewModelFactory.Create(http, login, agentEventsHttp);
        var window = new MainWindow(http, fakeServers) { DataContext = viewModel };
        window.CreateWorkspaceEventHttpClient = url =>
        {
            var client = ServerSessionProvider.CreateClient(
                new Uri(url),
                new FakeServerMessageHandler(fakeBackend, new HttpClientHandler()));
            client.Timeout = Timeout.InfiniteTimeSpan;
            return client;
        };
        return (window, viewModel);
    }
}
