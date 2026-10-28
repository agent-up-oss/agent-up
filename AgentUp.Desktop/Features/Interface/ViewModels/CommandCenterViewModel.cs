using System.Reactive;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Workspaces.Controllers;
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
    private readonly WorkspacesController? _workspaces;
    private readonly GitController? _git;
    private bool _active;
    private bool _persists = true;
    private AgentPageViewModel? _agent;

    public CommandCenterViewModel(
        InterfaceController controller,
        BridgeViewModel? bridge = null,
        TaskQueueViewModel? tasks = null,
        WorkspacesController? workspaces = null,
        GitController? git = null)
    {
        _controller = controller;
        _workspaces = workspaces;
        _git = git;
        _active = controller.UsesCommandCenter;
        Bridge = bridge;
        Tasks = tasks;
        ToggleCommand = ReactiveCommand.Create(Toggle);
        CloseAgentCommand = ReactiveCommand.Create(CloseAgent);
        OpenAgentCommand = ReactiveCommand.CreateFromTask<TeamStanding>(standing => OpenAgentAsync(standing));
    }

    /// <summary>
    /// The agent whose own page is open, or null while the team is the thing on screen. Opening
    /// one is how the team stops being a list of what everyone is doing and becomes the workspace
    /// they are doing it in.
    /// </summary>
    public AgentPageViewModel? Agent
    {
        get => _agent;
        private set
        {
            this.RaiseAndSetIfChanged(ref _agent, value);
            this.RaisePropertyChanged(nameof(HasAgent));
            this.RaisePropertyChanged(nameof(ShowsSurfaces));
        }
    }

    public bool HasAgent => Agent is not null;

    /// <summary>True while the bridge and the queue are what the main pane draws.</summary>
    public bool ShowsSurfaces => Agent is null;

    public ReactiveCommand<Unit, Unit> CloseAgentCommand { get; }

    /// <summary>Opens the agent a rail row names.</summary>
    public ReactiveCommand<TeamStanding, Unit> OpenAgentCommand { get; }

    /// <summary>
    /// Opens one agent's own page. The reads behind it are optional, because the headless tests
    /// compose this shell without them; without them a member simply does not open.
    /// </summary>
    public async Task OpenAgentAsync(TeamStanding standing, CancellationToken cancellationToken = default)
    {
        if (_workspaces is null || _git is null) return;
        var page = new AgentPageViewModel(standing, _workspaces, _git);
        Agent = page;
        await page.ReadAsync(cancellationToken);
    }

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
