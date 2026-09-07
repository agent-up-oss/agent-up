using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Views;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using System.Net.Http.Headers;

namespace AgentUp.Desktop.Composition;

public static class AppComposition
{
    public static async Task InitializeDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var http = CreateServerHttpClient();
        var authentication = new AuthenticationApiClient(http);
        if (!await AuthenticateAsync(desktop, http, authentication)) return;

        var (window, viewModel) = CreateMainWindow(http);
        desktop.MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }

    private static HttpClient CreateServerHttpClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000")
    };

    private static async Task<bool> AuthenticateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        HttpClient http,
        AuthenticationApiClient authentication)
    {
        if (!await authentication.IsAuthenticationRequiredAsync()) return true;

        var login = new LoginWindow(authentication);
        desktop.MainWindow = login;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        login.Closed += (_, _) => closed.TrySetResult();
        login.Show();
        await closed.Task;
        if (string.IsNullOrWhiteSpace(login.AccessToken))
        {
            desktop.Shutdown();
            return false;
        }

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return true;
    }

    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(HttpClient http)
    {
        var viewModel = MainViewModelFactory.Create(http);
        return (new MainWindow(http) { DataContext = viewModel }, viewModel);
    }
}
