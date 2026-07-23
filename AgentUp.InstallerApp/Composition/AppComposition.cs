using Avalonia.Controls;
using AgentUp.InstallerApp.Features.Installation.Controllers;
using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.InstallerApp.Features.Installation.Views;

namespace AgentUp.InstallerApp.Composition;

public static class AppComposition
{
    public static Window CreateInstallerWindow()
        => new InstallerWindow { DataContext = InstallerViewModelFactory.CreateDefault() };

    public static InstallerCommandLineController CreateCommandLineController()
        => new InstallerCommandLineController(new InstallerCommandLineService());
}
