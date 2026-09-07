using Avalonia.Controls;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Composition;

public static class AppComposition
{
    public static (Window Window, MainViewModel ViewModel) CreateMainWindow(HttpClient http)
    {
        var viewModel = MainViewModelFactory.Create(http);
        return (new MainWindow(http) { DataContext = viewModel }, viewModel);
    }
}
