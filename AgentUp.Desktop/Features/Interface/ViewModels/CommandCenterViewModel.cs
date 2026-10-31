using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
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
    private TeamStanding? _agent;

    public CommandCenterViewModel(
        InterfaceController controller,
        BridgeViewModel? bridge = null,
        TaskQueueViewModel? tasks = null,
        TrainingViewModel? training = null,
        TaskSourceViewModel? taskSource = null)
    {
        _controller = controller;
        _active = controller.UsesCommandCenter;
        Bridge = bridge;
        Tasks = tasks;
        Training = training;
        TaskSource = taskSource;
        ToggleCommand = ReactiveCommand.Create(Toggle);
        CloseAgentCommand = ReactiveCommand.Create(CloseAgent);
        OpenAgentCommand = ReactiveCommand.Create<TeamStanding>(OpenAgent);
    }

    /// <summary>
    /// The agent whose workspace is open, or null while the team is the thing on screen. Opening
    /// one is how the team stops being a list of what everyone is doing and becomes the workspace
    /// they are doing it in: the window hands the pane to that workspace's own detail, the same
    /// one the classic shell draws, with its tabs and everything under them.
    /// </summary>
    public TeamStanding? Agent
    {
        get => _agent;
        private set
        {
            this.RaiseAndSetIfChanged(ref _agent, value);
            this.RaisePropertyChanged(nameof(HasAgent));
            this.RaisePropertyChanged(nameof(ShowsSurfaces));
            this.RaisePropertyChanged(nameof(ShowsOwnSurfaces));
            this.RaisePropertyChanged(nameof(ShowsClassicLayout));
            this.RaisePropertyChanged(nameof(AgentName));
        }
    }

    public bool HasAgent => Agent is not null;

    /// <summary>The agent whose workspace is on screen, for the line above their tabs.</summary>
    public string AgentName => Agent?.DisplayName ?? string.Empty;

    /// <summary>True while the bridge and the queue are what the main pane draws.</summary>
    public bool ShowsSurfaces => Agent is null;

    /// <summary>
    /// True while the command center draws its own surfaces. With an agent open it does not: the
    /// workspace detail underneath is the thing on screen, so this shell steps out of its way.
    /// </summary>
    public bool ShowsOwnSurfaces => Active && Agent is null;

    /// <summary>
    /// True while the window draws the classic layout: always under the classic shell, and under
    /// the command center once an agent is open, because that agent's workspace is drawn by the
    /// very same detail rather than by a copy of it.
    /// </summary>
    public bool ShowsClassicLayout => !Active || Agent is not null;

    public ReactiveCommand<Unit, Unit> CloseAgentCommand { get; }

    /// <summary>Opens the agent a rail row names.</summary>
    public ReactiveCommand<TeamStanding, Unit> OpenAgentCommand { get; }

    /// <summary>
    /// Opens one agent's workspace. Nothing is read here: naming the agent is the whole of it,
    /// and the window answers by selecting that workspace and showing its detail.
    /// </summary>
    public void OpenAgent(TeamStanding standing) => Agent = standing;

    private void CloseAgent() => Agent = null;

    /// <summary>
    /// Triage for the whole team, or null where the shell was composed without the reads behind it
    /// (the headless tests). The classic shell never touches it.
    /// </summary>
    public BridgeViewModel? Bridge { get; }

    /// <summary>
    /// The queue the agents work from, or null where the shell was composed without the reads
    /// behind it (the headless tests). The classic shell never touches it.
    /// </summary>
    public TaskQueueViewModel? Tasks { get; }

    /// <summary>
    /// The decisions the agents took and the verdicts you give them, or null where the shell was
    /// composed without the reads behind it.
    /// </summary>
    public TrainingViewModel? Training { get; }

    /// <summary>
    /// Where this Server's agents take their work from, or null where the shell was composed
    /// without the reads behind it.
    /// </summary>
    public TaskSourceViewModel? TaskSource { get; }

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
        // Leaving the command center leaves the agent it had open, so the classic shell comes
        // back whole rather than with its rail still stood down for a page that is gone.
        if (!Active) Agent = null;
        this.RaisePropertyChanged(nameof(Classic));
        this.RaisePropertyChanged(nameof(ToggleToolTip));
        this.RaisePropertyChanged(nameof(ShowsOwnSurfaces));
        this.RaisePropertyChanged(nameof(ShowsClassicLayout));
    }
}
