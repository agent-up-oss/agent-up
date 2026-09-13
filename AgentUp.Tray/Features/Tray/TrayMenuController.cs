using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Linq;

namespace AgentUp.Tray.Features.Tray;

/// <summary>
/// Owns the tray icon and its menu, and keeps both in step with the service state.
/// </summary>
/// <remarks>
/// The icon is built in the constructor and the graphic is supplied to <see cref="Attach"/>
/// instead, so the menu shape and the state handling can be exercised without a running
/// application: loading a <see cref="WindowIcon"/> needs a platform render backend, while
/// the menu objects themselves do not.
/// </remarks>
public sealed class TrayMenuController : IDisposable
{
    private readonly ServerConnectionManager _connection;
    private readonly Action _quit;
    private readonly NativeMenuItem _statusItem;
    private readonly NativeMenuItem _restartItem;
    private readonly TrayIcon _trayIcon;
    private IDisposable? _subscription;

    public TrayMenuController(ServerConnectionManager connection, Action quit)
    {
        _connection = connection;
        _quit = quit;

        _statusItem = new NativeMenuItem { IsEnabled = false };
        _restartItem = new NativeMenuItem("Restart") { IsEnabled = false };
        _restartItem.Click += async (_, _) => await _connection.RestartAsync();

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Agent-Up",
            Menu = BuildMenu()
        };
    }

    /// <summary>The configured tray icon this controller owns.</summary>
    public TrayIcon Icon => _trayIcon;

    /// <summary>The menu the tray icon shows, in display order.</summary>
    public NativeMenu Menu => (NativeMenu)_trayIcon.Menu!;

    public void Attach(Application app, WindowIcon icon)
    {
        _trayIcon.Icon = icon;
        TrayIcon.SetIcons(app, [_trayIcon]);

        _subscription = _connection.State
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ApplyState);
    }

    /// <summary>Renders a service state into the status line, tooltip and restart item.</summary>
    public void ApplyState(ServiceState state)
    {
        _statusItem.Header = TrayStatusText.MenuLine(state);
        _trayIcon.ToolTipText = TrayStatusText.ToolTip(state);
        _restartItem.IsEnabled = TrayStatusText.CanRestart(state);
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

    public void Dispose()
    {
        _subscription?.Dispose();
        _trayIcon.Dispose();
    }
}
