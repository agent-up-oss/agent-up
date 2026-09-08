using System.Net.Http.Headers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Composition;

public static class AppComposition
{
    public static async Task InitializeDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var http = CreateServerHttpClient();
        var authentication = new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)));
        var login = new LoginViewModel(authentication);
        var (window, viewModel) = CreateMainWindow(http, login);
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
                return true;

            login.Show();
            var token = await login.WaitForSignInAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                desktop.Shutdown();
                return false;
            }

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
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

    private static HttpClient CreateServerHttpClient()
        => new() { BaseAddress = SecureServerUrlProvider.ResolveServerUri() };

    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(HttpClient http, LoginViewModel login)
    {
        var viewModel = MainViewModelFactory.Create(http, login);
        return (new MainWindow(http) { DataContext = viewModel }, viewModel);
    }
}
