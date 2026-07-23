using Avalonia.Controls;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Composition;

public static class AppComposition
{
    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(string serverUrl)
    {
        var viewModel = MainViewModelFactory.Create(serverUrl);
        return (new MainWindow { DataContext = viewModel }, viewModel);
    }
}
