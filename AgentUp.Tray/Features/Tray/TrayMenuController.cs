using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Reactive.Linq;

namespace AgentUp.Tray.Features.Tray;

public sealed class TrayMenuController : IDisposable
{
    private readonly ServiceLifecycleManager _lifecycle;
    private readonly WindowIcon _icon;
    private readonly Action _quit;
    private readonly NativeMenuItem _statusItem;
    private readonly NativeMenuItem _pauseItem;
    private readonly NativeMenuItem _resumeItem;
    private readonly NativeMenuItem _restartItem;
    private IDisposable? _subscription;

    public TrayMenuController(ServiceLifecycleManager lifecycle, WindowIcon icon, Action quit)
    {
        _lifecycle = lifecycle;
        _icon = icon;
        _quit = quit;

        _statusItem = new NativeMenuItem { IsEnabled = false };
        _pauseItem = new NativeMenuItem("Pause");
        _resumeItem = new NativeMenuItem("Resume") { IsVisible = false };
        _restartItem = new NativeMenuItem("Restart") { IsEnabled = false };
        var quitItem = new NativeMenuItem("Quit");

        _pauseItem.Click += async (_, _) => await _lifecycle.PauseAsync();
        _resumeItem.Click += async (_, _) => await _lifecycle.ResumeAsync();
        _restartItem.Click += async (_, _) => await _lifecycle.RestartAsync();
        quitItem.Click += async (_, _) =>
        {
            await _lifecycle.QuitAsync();
            _quit();
        };

        var menu = new NativeMenu();
        menu.Add(_statusItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_pauseItem);
        menu.Add(_resumeItem);
        menu.Add(_restartItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);
    }

    public void Attach(Application app)
    {
        var trayIcon = new TrayIcon
        {
            ToolTipText = "Agent-Up",
            Icon = _icon,
            Menu = BuildMenu()
        };

        TrayIcon.SetIcons(app, [trayIcon]);

        _subscription = _lifecycle.State
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(state => ApplyState(state, trayIcon));
    }

    private NativeMenu BuildMenu()
    {
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += async (_, _) =>
        {
            await _lifecycle.QuitAsync();
            _quit();
        };

        var menu = new NativeMenu();
        menu.Add(_statusItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_pauseItem);
        menu.Add(_resumeItem);
        menu.Add(_restartItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(quitItem);
        return menu;
    }

    private void ApplyState(ServiceState state, TrayIcon trayIcon)
    {
        _statusItem.Header = StatusLine(state);
        trayIcon.ToolTipText = $"Agent-Up · {StatusWord(state)}";

        var running = state == ServiceState.Running;
        var paused = state == ServiceState.Paused;

        _pauseItem.IsVisible = !paused;
        _pauseItem.IsEnabled = running;
        _resumeItem.IsVisible = paused;
        _restartItem.IsEnabled = running || paused || state == ServiceState.Failed;
    }

    private static string StatusLine(ServiceState state) => state switch
    {
        ServiceState.Starting => "Agent-Up  Starting...",
        ServiceState.Running => "Agent-Up  Running",
        ServiceState.Paused => "Agent-Up  Paused",
        ServiceState.Restarting => "Agent-Up  Restarting...",
        ServiceState.Failed => "Agent-Up  Service failed",
        _ => "Agent-Up  Stopped"
    };

    private static string StatusWord(ServiceState state) => state switch
    {
        ServiceState.Starting => "Starting...",
        ServiceState.Running => "Running",
        ServiceState.Paused => "Paused",
        ServiceState.Restarting => "Restarting...",
        ServiceState.Failed => "Failed",
        _ => "Stopped"
    };

    public void Dispose() => _subscription?.Dispose();
}
