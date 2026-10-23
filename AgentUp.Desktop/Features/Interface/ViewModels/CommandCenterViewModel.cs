using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// Which shell the window draws, and the switch between them.
///
/// The two shells are alternatives rather than layers: when this says classic, the window shows
/// exactly the tree it showed before this setting existed. The command center's own Settings
/// surface binds the same command, so the switch is reachable from both sides.
/// </summary>
public sealed class CommandCenterViewModel : ReactiveObject
{
    private readonly InterfaceController _controller;
    private bool _active;
    private bool _persists = true;

    public CommandCenterViewModel(InterfaceController controller)
    {
        _controller = controller;
        _active = controller.UsesCommandCenter;
        ToggleCommand = ReactiveCommand.Create(Toggle);
    }

    /// <summary>True while the command center is the shell on screen.</summary>
    public bool Active
    {
        get => _active;
        private set => this.RaiseAndSetIfChanged(ref _active, value);
    }

    public bool Classic => !Active;

    /// <summary>False once a switch could not be written, so the surface can say it will not survive a restart.</summary>
    public bool Persists
    {
        get => _persists;
        private set => this.RaiseAndSetIfChanged(ref _persists, value);
    }

    public string ToggleToolTip => Active
        ? "Switch back to the classic interface"
        : "Try the command center interface";

    public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

    private void Toggle()
    {
        Persists = _controller.Toggle();
        Active = _controller.UsesCommandCenter;
        this.RaisePropertyChanged(nameof(Classic));
        this.RaisePropertyChanged(nameof(ToggleToolTip));
    }
}
