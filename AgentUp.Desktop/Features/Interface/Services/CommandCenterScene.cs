using AgentUp.Desktop.DesignSystem;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>One task row the scene repeats. The action is the catalog button the row runs.</summary>
public sealed record CommandCenterTaskSlot(
    string Id,
    string Title,
    string Repository,
    string Agent,
    string Action,
    string ActionLabel);

/// <summary>One recorded validation flow the rail and the dock repeat.</summary>
public sealed record CommandCenterFlowSlot(string Id, string Name, string Description);

/// <summary>What the command center knows when it fills the scene. The scene owns the page.</summary>
public sealed record CommandCenterSceneInput(
    string SceneId,
    string ServerLabel,
    string? OpenWorkspaceId,
    bool ValidationsOpen,
    bool ValidationRunning,
    bool ValidationPassed,
    string ValidationSummary,
    IReadOnlyList<TeamStanding> Team,
    IReadOnlyList<CommandCenterTaskSlot> Tasks,
    IReadOnlyList<CommandCenterFlowSlot> Flows);

/// <summary>Text, repeated rows, and states for one scene. The view applies this and nothing else.</summary>
public sealed class CommandCenterSceneBinding
{
    public Dictionary<string, string> Text { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, IReadOnlyList<NewUiSlotItem>> Items { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> State { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Fills the generated command-center scene from Server state.
/// Workspace LEDs, branch lines, lane titles, and the fleet headline are the catalog's.
/// </summary>
public static class CommandCenterScene
{
    public static CommandCenterSceneBinding Bind(CommandCenterSceneInput input)
    {
        var binding = new CommandCenterSceneBinding();
        var team = input.Team;
        binding.Text["headline"] = Headline(team);
        binding.Text["total"] = team.Count.ToString();
        binding.Text["totalLabel"] = team.Count == 1 ? "AGENT" : "AGENTS";
        binding.Text["meta"] = FleetMeta(team);
        binding.Text["server"] = input.ServerLabel;
        binding.Text["summary"] = input.ValidationSummary;
        binding.Items["workspaces"] = team.Select(standing => Workspace(standing, input.OpenWorkspaceId)).ToArray();
        binding.Items["fleet"] = Lanes(team).ToArray();
        binding.Items["queue"] = input.Tasks.Select(Task).ToArray();
        binding.Items["flows"] = input.Flows.Select(Flow).ToArray();
        binding.State["nav"] = Nav(input.SceneId);
        binding.State["selected"] = Selected(input.SceneId);
        if (input.ValidationsOpen) binding.State["open"] = "1";
        if (input.ValidationRunning) binding.State["running"] = "1";
        if (input.ValidationPassed) binding.State["passed"] = "1";
        return binding;
    }

    public static string Headline(IReadOnlyList<TeamStanding> team)
    {
        var working = team.Count(standing => standing.Kind == TeamStandingKind.Working);
        var idle = team.Count(standing => standing.Kind == TeamStandingKind.Idle);
        var waiting = team.Count(standing => standing.Kind == TeamStandingKind.WaitingOnYou);
        var halted = team.Count(standing => standing.Kind == TeamStandingKind.Halted);
        var parts = new List<string> { $"{team.Count} {(team.Count == 1 ? "agent" : "agents")}" };
        if (working > 0) parts.Add($"{working} working");
        if (idle > 0) parts.Add($"{idle} idle");
        if (waiting > 0) parts.Add($"{waiting} waiting for approval");
        if (halted > 0) parts.Add($"{halted} {(halted == 1 ? "error" : "errors")}");
        return string.Join(" · ", parts);
    }

    private static string FleetMeta(IReadOnlyList<TeamStanding> team)
    {
        var working = team.Count(standing => standing.Kind == TeamStandingKind.Working);
        var idle = team.Count(standing => standing.Kind == TeamStandingKind.Idle);
        var attention = team.Count(standing => standing.Kind is TeamStandingKind.WaitingOnYou or TeamStandingKind.Halted);
        var parts = new List<string> { $"{working} active", $"{idle} ready" };
        if (attention > 0) parts.Add($"{attention} need attention");
        return string.Join(" · ", parts);
    }

    private static NewUiSlotItem Workspace(TeamStanding standing, string? openId)
    {
        var parts = new HashSet<string>(StringComparer.Ordinal) { Part(standing.Kind) };
        if (standing.Branch.Length > 0) parts.Add("branch");
        return new NewUiSlotItem
        {
            Id = standing.WorkspaceId,
            State = standing.WorkspaceId == openId ? "au-workspace--selected" : null,
            Parts = parts,
            Text = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = standing.DisplayName,
                ["branch"] = standing.Branch,
            },
        };
    }

    private static IEnumerable<NewUiSlotItem> Lanes(IReadOnlyList<TeamStanding> team)
    {
        foreach (var (kind, title, meta, state) in LaneCopy)
        {
            var members = team.Where(standing => standing.Kind == kind).ToArray();
            if (members.Length == 0) continue;
            yield return new NewUiSlotItem
            {
                Id = state,
                State = state,
                Text = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = title,
                    ["meta"] = meta,
                    ["count"] = members.Length.ToString(),
                },
                Items = new Dictionary<string, List<NewUiSlotItem>>(StringComparer.Ordinal)
                {
                    ["hosts"] = members.Select(Host).ToList(),
                },
            };
        }
    }

    private static NewUiSlotItem Host(TeamStanding standing)
    {
        var parts = new HashSet<string>(StringComparer.Ordinal);
        var text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = standing.DisplayName,
            ["task"] = standing.TaskLabel ?? "No task in hand",
        };
        if (standing.Reason is { Length: > 0 } reason)
        {
            parts.Add("messages");
            text["message"] = reason;
        }
        return new NewUiSlotItem
        {
            Id = standing.WorkspaceId,
            Parts = parts,
            Text = text,
        };
    }

    private static NewUiSlotItem Task(CommandCenterTaskSlot task)
    {
        var parts = new HashSet<string>(StringComparer.Ordinal) { "grip" };
        if (task.Agent.Length > 0) parts.Add("agent");
        return new NewUiSlotItem
        {
            Id = task.Id,
            Action = task.Action,
            Parts = parts,
            Text = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = task.Title,
                ["repo"] = task.Repository,
                ["agent"] = task.Agent,
                ["action"] = task.ActionLabel,
            },
        };
    }

    private static NewUiSlotItem Flow(CommandCenterFlowSlot flow) => new()
    {
        Id = flow.Id,
        Action = "run",
        Parts = new HashSet<string>(StringComparer.Ordinal) { "play" },
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = flow.Name,
            ["description"] = flow.Description,
        },
    };

    private static string Part(TeamStandingKind kind) => kind switch
    {
        TeamStandingKind.Working => "working",
        TeamStandingKind.WaitingOnYou => "waiting",
        TeamStandingKind.Halted => "halted",
        _ => "idle",
    };

    private static string Nav(string sceneId) => sceneId switch
    {
        "tasks" or "mobile-tasks" => "1",
        "settings" or "mobile-settings" => "2",
        _ => "0",
    };

    private static string Selected(string sceneId) => sceneId switch
    {
        "agent-chat" or "mobile-chat" => "1",
        "agent-git" or "mobile-agent-git" => "2",
        "application" or "mobile-application" => "3",
        "mobile-settings" => "2",
        "mobile-tasks" => "1",
        _ => "0",
    };

    private static readonly (TeamStandingKind Kind, string Title, string Meta, string State)[] LaneCopy =
    [
        (TeamStandingKind.WaitingOnYou, "Wait for approval", "Needs your decision", "aun-state-map__branch--waiting"),
        (TeamStandingKind.Halted, "Error", "Needs recovery", "aun-state-map__branch--halted"),
        (TeamStandingKind.Working, "Working", "Moving work forward", "aun-state-map__branch--working"),
        (TeamStandingKind.Idle, "Idle", "Ready for work", "aun-state-map__branch--idle"),
    ];
}
