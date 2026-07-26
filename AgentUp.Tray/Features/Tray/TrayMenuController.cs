using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Linq;

namespace AgentUp.Tray.Features.Tray;

public sealed class TrayMenuController : IDisposable
{
    private readonly ServerConnectionManager _connection;
    private readonly WindowIcon _icon;
    private readonly Action _quit;
    private readonly NativeMenuItem _statusItem;
    private readonly NativeMenuItem _restartItem;
    private TrayIcon? _trayIcon;
    private IDisposable? _subscription;

    public TrayMenuController(ServerConnectionManager connection, WindowIcon icon, Action quit)
    {
        _connection = connection;
        _icon = icon;
        _quit = quit;

        _statusItem = new NativeMenuItem { IsEnabled = false };
        _restartItem = new NativeMenuItem("Restart") { IsEnabled = false };

        _restartItem.Click += async (_, _) => await _connection.RestartAsync();
    }

    public void Attach(Application app)
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Agent-Up",
            Icon = _icon,
            Menu = BuildMenu()
        };

        TrayIcon.SetIcons(app, [_trayIcon]);

        _subscription = _connection.State
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(state => ApplyState(state, _trayIcon));
    }

    private NativeMenu BuildMenu()
    {
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += async (_, _) =>
        {
            await _connection.QuitAsync();
            _quit();
        };

        var menu = new NativeMenu();
        menu.Add(_statusItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_restartItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);
        return menu;
    }

    private void ApplyState(ServiceState state, TrayIcon trayIcon)
    {
        _statusItem.Header = StatusLine(state);
        trayIcon.ToolTipText = $"Agent-Up · {StatusWord(state)}";
        _restartItem.IsEnabled = state == ServiceState.Connected;
    }

    private static string StatusLine(ServiceState state) => state switch
    {
        ServiceState.Connecting => "Agent-Up  Connecting...",
        ServiceState.Connected => "Agent-Up  Running",
        ServiceState.Restarting => "Agent-Up  Restarting...",
        ServiceState.Disconnected => "Agent-Up  Disconnected",
        _ => "Agent-Up  Unknown"
    };

    private static string StatusWord(ServiceState state) => state switch
    {
        ServiceState.Connecting => "Connecting...",
        ServiceState.Connected => "Running",
        ServiceState.Restarting => "Restarting...",
        ServiceState.Disconnected => "Disconnected",
        _ => "Unknown"
    };

    public void Dispose()
    {
        _subscription?.Dispose();
        _trayIcon?.Dispose();
    }
}
