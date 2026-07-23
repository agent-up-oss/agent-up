using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AgentUp.InstallerApp.Composition;
using AgentUp.InstallerApp.Features.Logging.Tools;

namespace AgentUp.InstallerApp;

public class App : Application
{
    public override void Initialize()
    {
        InstallerLog.Write("Avalonia initializing");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InstallerLog.Write("Creating installer window");
            try
            {
                desktop.MainWindow = AppComposition.CreateInstallerWindow();
                InstallerLog.Write("Installer window created successfully");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                InstallerLog.WriteException("window initialization", ex);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
