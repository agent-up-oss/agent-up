using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AgentUp.Tray.Features.AutoStart;
using AgentUp.Tray.Features.Tray;

namespace AgentUp.Tray;

public class App : Application
{
    private ServerConnectionManager? _connection;
    private TrayMenuController? _menu;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var icon = LoadTrayIcon();
        _connection = new ServerConnectionManager();
        _menu = new TrayMenuController(_connection, icon,
            () => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(0));
        _menu.Attach(this);

        _ = _connection.StartAsync();

        AutoStartRegistrarFactory.Create()?.EnsureRegistered();

        base.OnFrameworkInitializationCompleted();
    }

    private static WindowIcon LoadTrayIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://AgentUp.Tray/Assets/logo.png"));
        using var bitmap = new Bitmap(stream);
        return new WindowIcon(bitmap);
    }
}
