using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using System.Net.Http.Headers;

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

        if (await authentication.IsRequiredAsync())
        {
            viewModel.Login.Show();
            var token = await viewModel.Login.WaitForSignInAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                desktop.Shutdown();
                return;
            }

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        await viewModel.InitializeAsync();
    }

    private static HttpClient CreateServerHttpClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000")
    };

    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(HttpClient http, LoginViewModel login)
    {
        var viewModel = MainViewModelFactory.Create(http, login);
        return (new MainWindow(http) { DataContext = viewModel }, viewModel);
    }
}
