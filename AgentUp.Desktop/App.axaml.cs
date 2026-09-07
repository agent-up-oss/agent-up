using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Views;
using System.Net.Http.Headers;

namespace AgentUp.Desktop;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = InitializeDesktopAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var serverUrl = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
        var http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        var authentication = new AuthenticationApiClient(http);
        if (await authentication.IsAuthenticationRequiredAsync())
        {
            var login = new LoginWindow(authentication);
            desktop.MainWindow = login;
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            login.Closed += (_, _) => closed.TrySetResult();
            login.Show();
            await closed.Task;
            if (string.IsNullOrWhiteSpace(login.AccessToken))
            {
                desktop.Shutdown();
                return;
            }
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        }

        var (window, viewModel) = AppComposition.CreateMainWindow(http);
        desktop.MainWindow = window;
        window.Show();
        await viewModel.InitializeAsync();
    }
}
