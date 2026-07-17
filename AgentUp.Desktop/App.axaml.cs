using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AgentUp.Desktop.Features.Workspaces.Factories;
using AgentUp.Desktop.Features.Workspaces.Views;

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
            var serverUrl = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
            var viewModel = MainViewModelFactory.Create(serverUrl);

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
