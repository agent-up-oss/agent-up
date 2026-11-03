using AgentUp.Desktop.DesignSystem;
using AgentUp.Desktop.Features.Agents.DTOs;
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
public sealed record CommandCenterFlowSlot(
    string Id,
    string Name,
    string Description,
    string Status = "pending",
    IReadOnlyList<CommandCenterStageSlot>? Stages = null);

/// <summary>One step of a recorded flow, and the expectations under it.</summary>
public sealed record CommandCenterStageSlot(
    string Id,
    string Label,
    string Status,
    IReadOnlyList<CommandCenterCheckSlot> Checks);

/// <summary>One expectation under a validation stage.</summary>
public sealed record CommandCenterCheckSlot(string Label, string Status);

/// <summary>One Settings field. A locked field still shows its control, and the control does nothing.</summary>
public sealed record CommandCenterSettingSlot(
    string Id,
    string Title,
    string Detail,
    string ActionLabel,
    string? Action,
    bool Enabled,
    IReadOnlyList<CommandCenterSettingSlot>? Children = null);

/// <summary>The agent properties Settings · Chat turns into one field each.</summary>
public sealed record CommandCenterChatSettings(
    IReadOnlyList<AutonomyPreconditionDto> Preconditions,
    IReadOnlyList<AgentDescriptorDto> Agents,
    string? BoundAgent,
    string? LoopDetail,
    bool CanEnableAutonomy,
    bool CanActivateLoop,
    bool CanCancelLoop,
    bool CanStartBound);

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
    IReadOnlyList<CommandCenterFlowSlot> Flows,
    string Address = "",
    string PortLabel = "",
    string Tool = "port",
    bool Browser = true,
    IReadOnlyList<string>? Tools = null,
    bool ValidationCollapsed = true,
    string? SettingsWorkspaceId = null,
    IReadOnlyList<CommandCenterSettingSlot>? ChatSettings = null,
    string? SelectedApplication = null);

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
        binding.Text["address"] = input.Address;
        if (input.PortLabel.Length > 0) binding.Text["port"] = input.PortLabel;
        binding.Items["workspaces"] = team.Select(standing => Workspace(standing, input.OpenWorkspaceId)).ToArray();
        binding.Items["settingsWorkspaces"] = team.Select(standing => SettingsWorkspace(standing, input.SettingsWorkspaceId ?? input.OpenWorkspaceId)).ToArray();
        binding.Items["fleet"] = Lanes(team).ToArray();
        binding.Items["queue"] = input.Tasks.Select(Task).ToArray();
        binding.Items["flows"] = input.Flows.Select(Flow).ToArray();
        binding.Items["chatFields"] = (input.ChatSettings ?? []).Select(Setting).ToArray();
        binding.State["nav"] = Nav(input.SceneId);
        binding.State["selected"] = Selected(input.SceneId);
        binding.State["tool"] = input.Tool;
        if (input.Browser) binding.State["browser"] = "1";
        if (input.Tools is not null)
        {
            foreach (var toolName in input.Tools)
                binding.State[toolName] = "1";
        }
        if (input.Flows.Count > 0) binding.State["checks"] = "1";
        if (input.SceneId is "application" or "mobile-application" && !string.IsNullOrEmpty(input.SelectedApplication))
            binding.State["app"] = input.SelectedApplication;
        if (input.ValidationCollapsed)
        {
            binding.State["collapsed"] = "1";
            binding.Text["validationToggle"] = "‹";
        }
        else
        {
            binding.State["expanded"] = "1";
            binding.Text["validationToggle"] = "›";
        }
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

    private static NewUiSlotItem SettingsWorkspace(TeamStanding standing, string? selectedId) => new()
    {
        Id = standing.WorkspaceId,
        State = standing.WorkspaceId == selectedId ? "au-app-tab--selected" : null,
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = standing.DisplayName,
        },
    };

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
        State = Dot(flow.Status),
        Parts = new HashSet<string>(StringComparer.Ordinal) { "play" },
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = flow.Name,
            ["description"] = flow.Description,
        },
        Items = new Dictionary<string, List<NewUiSlotItem>>
        {
            ["stages"] = (flow.Stages ?? []).Select(Stage).ToList(),
        },
    };

    private static NewUiSlotItem Stage(CommandCenterStageSlot stage) => new()
    {
        Id = stage.Id,
        State = Dot(stage.Status),
        Text = new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = stage.Label },
        Items = new Dictionary<string, List<NewUiSlotItem>>
        {
            ["checks"] = stage.Checks.Select(Check).ToList(),
        },
    };

    private static NewUiSlotItem Check(CommandCenterCheckSlot check) => new()
    {
        State = Dot(check.Status),
        Text = new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = check.Label },
    };

    private static string Dot(string status) => status switch
    {
        "passed" => "au-status-dot--healthy",
        "failed" => "au-status-dot--danger",
        "running" => "au-status-dot--warning",
        _ => "au-status-dot--idle",
    };

    public static IReadOnlyList<CommandCenterSettingSlot> ChatFields(CommandCenterChatSettings settings)
    {
        var agents = settings.Agents.Select(agent => AgentRow(agent, settings.BoundAgent)).ToArray();
        var rows = settings.Preconditions.Select(item => Row(item, settings, agents)).ToList();
        if (agents.Length > 0 && settings.Preconditions.All(item => item.Id != "agentAvailable"))
            rows.Add(new CommandCenterSettingSlot("agents", "Agent", "Choose an agent", "Choose", null, false, agents));
        if (settings.Preconditions.All(item => item.Id != "autonomyEnabled"))
            rows.Add(Loop(settings));
        return rows;
    }

    private static CommandCenterSettingSlot Row(
        AutonomyPreconditionDto item,
        CommandCenterChatSettings settings,
        IReadOnlyList<CommandCenterSettingSlot> agents) => item.Id switch
    {
        "autonomyEnabled" => new(item.Id, item.Label, item.Status, "Enable", "enable-autonomy", !item.Satisfied && settings.CanEnableAutonomy, [Loop(settings)]),
        "agentAvailable" => new(item.Id, item.Label, item.Status, "Choose", null, false, agents),
        "boundAgent" => new(item.Id, item.Label, item.Status, "Start", "start-bound", settings.CanStartBound),
        "hostConnectionAuthorized" => Locked(item, "Connect"),
        "forgeCapabilityEnabled" => Locked(item, "Enable"),
        "workspaceRegistered" => Locked(item, "Register"),
        "permissionRequests" => Locked(item, "Require"),
        "permissionPolicy" => Locked(item, "Set"),
        "gitBranch" => Locked(item, "Checkout"),
        "unpushedCommits" => Locked(item, "Push"),
        "commitQueueEdit" => Locked(item, "Finish"),
        "unassignedChanges" => Locked(item, "Review"),
        _ => Locked(item, "Change"),
    };

    private static CommandCenterSettingSlot Locked(AutonomyPreconditionDto item, string label)
        => new(item.Id, item.Label, item.Status, label, null, false);

    private static CommandCenterSettingSlot AgentRow(AgentDescriptorDto agent, string? boundAgent)
    {
        var bound = string.Equals(agent.Agent, boundAgent, StringComparison.Ordinal);
        if (agent.Available)
            return new(agent.Agent, agent.DisplayName, bound ? "Bound" : agent.StatusLabel, bound ? "Bound" : "Use", "select-agent", !bound);
        if (agent.CanEnable)
            return new(agent.Agent, agent.DisplayName, agent.Remedy ?? agent.StatusLabel, "Enable", "enable-agent", true);
        return new(agent.Agent, agent.DisplayName, agent.Remedy ?? agent.StatusLabel, "Enable", null, false);
    }

    private static CommandCenterSettingSlot Loop(CommandCenterChatSettings settings)
    {
        var detail = string.IsNullOrWhiteSpace(settings.LoopDetail) ? "Not running" : settings.LoopDetail;
        return new("loop", "Unattended loop", detail, "Activate", "activate-loop", settings.CanActivateLoop,
        [
            new("cancel-loop", "Stop the loop", "Stops the running loop", "Cancel", "cancel-loop", settings.CanCancelLoop),
        ]);
    }

    private static NewUiSlotItem Setting(CommandCenterSettingSlot slot)
    {
        var parts = new HashSet<string>(StringComparer.Ordinal) { "control" };
        if (!slot.Enabled) parts.Add("locked");
        return new NewUiSlotItem
        {
            Id = slot.Id,
            Action = slot.Enabled ? slot.Action : null,
            Parts = parts,
            Text = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = slot.Title,
                ["detail"] = slot.Detail,
                ["action"] = slot.ActionLabel,
            },
            Items = slot.Children is { Count: > 0 }
                ? new Dictionary<string, List<NewUiSlotItem>> { ["fields"] = slot.Children.Select(Setting).ToList() }
                : new Dictionary<string, List<NewUiSlotItem>>(),
        };
    }

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
