using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// Triage for the whole team. It groups the standings the Server reports and says plainly when a
/// group is empty, because an empty triage list is the good outcome and must not read as a failure
/// to load.
/// </summary>
public sealed class BridgeViewModel : ReactiveObject
{
    private readonly TeamStandingController _standings;
    /// <summary>Counts reads so a superseded one can tell it is no longer the newest.</summary>
    private int _reads;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _hasRead;

    public BridgeViewModel(TeamStandingController standings)
    {
        _standings = standings;
        RefreshCommand = ReactiveCommand.CreateFromTask(() => ReadAsync());
    }

    /// <summary>Gates: a run stopped and will not move until the user answers.</summary>
    public ObservableCollection<StandingRowViewModel> WaitingOnYou { get; } = [];

    /// <summary>Faults: a run ended and someone has to look.</summary>
    public ObservableCollection<StandingRowViewModel> Halted { get; } = [];

    /// <summary>
    /// Everyone, in the order the sidebar lists them. The rail draws from this; the bridge does
    /// not, because the bridge shows each agent once under what they need rather than twice.
    /// </summary>
    public ObservableCollection<StandingRowViewModel> Team { get; } = [];

    /// <summary>The rest of the team: running or idle, and needing nothing from you.</summary>
    public ObservableCollection<StandingRowViewModel> NothingNeeded { get; } = [];

    /// <summary>One row per task, with everyone on it.</summary>
    public ObservableCollection<TaskRowViewModel> Tasks { get; } = [];

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool HasError => ErrorMessage is { Length: > 0 };

    public bool HasWaiting => WaitingOnYou.Count > 0;
    public bool HasHalted => Halted.Count > 0;
    public bool HasTeam => Team.Count > 0;
    public bool HasNothingNeeded => NothingNeeded.Count > 0;
    public bool HasTasks => Tasks.Count > 0;

    /// <summary>True once a read finished, so an empty surface can tell apart "nothing" from "not yet".</summary>
    public bool HasRead
    {
        get => _hasRead;
        private set => this.RaiseAndSetIfChanged(ref _hasRead, value);
    }

    public string Headline => HasRead
        ? $"{Team.Count} {Plural(Team.Count, "agent")} · {WaitingOnYou.Count} waiting on you · {Halted.Count} halted"
        : "Reading the team…";

    /// <summary>
    /// Why the queue can be shorter than the forge's own issue list. The Server exposes the
    /// assigned-issue queue per workspace to the agent that works it, and nowhere else, so a client
    /// can only show the tasks agents report holding.
    /// </summary>
    public string TasksCaveat =>
        "These are the tasks the agents report holding. Unassigned issues live on the repository host; "
        + "this Server does not expose its queue to clients, so they are not listed here.";

    public string EmptyTeamHint => "This server has no workspaces yet.";

    /// <summary>
    /// Every workspace on this Server as numbers.
    ///
    /// A group nobody is in is not drawn, so a quiet Server would otherwise be a blank page. This
    /// is what fills it, and it is worth reading when the Server is busy too: counts rather than
    /// sentences, because the board is read at a glance.
    /// </summary>
    public ObservableCollection<FleetTileViewModel> Fleet { get; } = [];

    /// <summary>
    /// Reads the team. Reads overlap: the timer starts one every ten seconds, and the workspace
    /// list starts one each time it changes — which a reorder does once per item, since rebuilding
    /// the collection raises the change for every add. Nothing awaits them, so a slower earlier
    /// read can finish last. Only the newest read is allowed to say what the team looks like, or
    /// an older one would draw its stale list over the newer one and clear the loading flag while
    /// a read is still out.
    /// </summary>
    public async Task ReadAsync(IReadOnlyList<TeamMember>? members = null, CancellationToken cancellationToken = default)
    {
        var requested = members ?? Team.Select(row => row.Member).ToArray();
        var read = ++_reads;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var standings = await _standings.ReadAsync(requested, cancellationToken);
            if (read != _reads) return;
            // Before Apply, which raises Headline: the headline reads HasRead.
            HasRead = true;
            Apply(standings);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (read != _reads) return;
            ErrorMessage = exception.Message;
            this.RaisePropertyChanged(nameof(HasError));
        }
        finally
        {
            if (read == _reads) IsLoading = false;
        }
    }

    private void Apply(IReadOnlyList<TeamStanding> standings)
    {
        Replace(Team, standings);
        Replace(WaitingOnYou, standings.Where(standing => standing.Kind == TeamStandingKind.WaitingOnYou));
        Replace(Halted, standings.Where(standing => standing.Kind == TeamStandingKind.Halted));
        Replace(NothingNeeded, standings.Where(standing =>
            standing.Kind is not (TeamStandingKind.WaitingOnYou or TeamStandingKind.Halted)));
        Tasks.Clear();
        foreach (var task in Queue(standings)) Tasks.Add(task);
        Fleet.Clear();
        foreach (var tile in FleetTiles(standings)) Fleet.Add(tile);
        foreach (var name in new[]
                 {
                     nameof(HasWaiting), nameof(HasHalted), nameof(HasTeam), nameof(HasTasks),
                     nameof(HasNothingNeeded), nameof(Headline), nameof(HasError)
                 })
        {
            this.RaisePropertyChanged(name);
        }
    }

    /// <summary>
    /// The fleet counted from the standings the Server already answered, so the board costs no
    /// read of its own.
    /// </summary>
    private static IEnumerable<FleetTileViewModel> FleetTiles(IReadOnlyList<TeamStanding> standings)
    {
        var of = (TeamStandingKind kind) => standings.Count(standing => standing.Kind == kind);

        yield return new FleetTileViewModel("Agents", standings.Count);
        yield return new FleetTileViewModel("Working", of(TeamStandingKind.Working));
        yield return new FleetTileViewModel("Idle", of(TeamStandingKind.Idle));
        yield return new FleetTileViewModel("Holding a task", standings.Count(standing => standing.TaskNumber is not null));
        yield return new FleetTileViewModel("Waiting on you", of(TeamStandingKind.WaitingOnYou), FleetTone.Warning);
        yield return new FleetTileViewModel("Halted", of(TeamStandingKind.Halted), FleetTone.Danger);
        yield return new FleetTileViewModel(
            "Proposals open",
            standings.Count(standing => standing.Forge.ProposalNumber is not null));
        yield return new FleetTileViewModel("Checks running", standings.Sum(standing => standing.Forge.ChecksRunning));
        yield return new FleetTileViewModel(
            "Checks failed",
            standings.Sum(standing => standing.Forge.ChecksFailed),
            FleetTone.Danger);
        yield return new FleetTileViewModel(
            "Threads unresolved",
            standings.Sum(standing => standing.Forge.UnresolvedThreads),
            FleetTone.Warning);
    }

    /// <summary>
    /// The queue: one row per task, most urgent first, with every agent on that task. An issue
    /// three agents hold is one piece of work, not three.
    /// </summary>
    private static IEnumerable<TaskRowViewModel> Queue(IReadOnlyList<TeamStanding> standings)
        => standings
            .Where(standing => standing.TaskNumber is not null)
            .GroupBy(standing => standing.TaskNumber!.Value)
            .Select(task => new TaskRowViewModel(task.Key, [.. task]))
            .OrderBy(task => task.Rank)
            .ThenBy(task => task.Number);

    private static void Replace(ObservableCollection<StandingRowViewModel> target, IEnumerable<TeamStanding> standings)
    {
        target.Clear();
        foreach (var standing in standings) target.Add(new StandingRowViewModel(standing));
    }

    private static string Plural(int count, string word) => count == 1 ? word : $"{word}s";
}
