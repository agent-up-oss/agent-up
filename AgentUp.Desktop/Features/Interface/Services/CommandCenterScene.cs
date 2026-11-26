using AgentUp.Desktop.DesignSystem;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>One team member a round banner can name.</summary>
public sealed record CommandCenterMemberSlot(string Id, string Name, string Initials);

/// <summary>One repository the Tasks page can be drilled into.</summary>
public sealed record CommandCenterRepositorySlot(
    string Id,
    string Name,
    string Host,
    IReadOnlyList<CommandCenterMemberSlot>? Members = null);

/// <summary>One task row the scene repeats. The action is the catalog button the row runs; the scene words and tones it.</summary>
public sealed record CommandCenterTaskSlot(
    string Id,
    string Title,
    string Repository,
    string Agent,
    string Action,
    string Initials = "");

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

/// <summary>The autonomous toggle and the agents this member can run.</summary>
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
    string? SelectedApplication = null,
    IReadOnlyList<CommandCenterModuleSlot>? Modules = null,
    IReadOnlyList<CommandCenterApplicationSlot>? Applications = null,
    IReadOnlyList<CommandCenterTaskSlot>? DevReviews = null,
    IReadOnlyList<CommandCenterTaskSlot>? PmReviews = null,
    string TasksTab = "issues",
    IReadOnlyList<CommandCenterRepositorySlot>? Repositories = null,
    string? OpenRepository = null,
    string ForgeTab = "github",
    string ForgeDetail = "",
    string ForgeEndpoint = "",
    bool ForgeConnected = false,
    string AuthTab = "",
    string MemberKind = "Author",
    string BoundAgent = "",
    string AgentEndpoint = "",
    string AgentModel = "");

/// <summary>One application of the open workspace, as its tab shows it.</summary>
public sealed record CommandCenterApplicationSlot(string Name, bool Running);

/// <summary>One capability module on the Server, as Settings lists it.</summary>
/// <param name="Available">Whether the Server found the software the capability runs; one that is not offers no Enable.</param>
public sealed record CommandCenterModuleSlot(string Id, string DisplayName, bool Enabled, bool CanRun, string State, bool Available = true);

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
        var authTab = input.AuthTab.Length > 0
            ? input.AuthTab
            : team.Count > 0 ? team[0].WorkspaceId : "";
        binding.Items["authMembers"] = team.Select(standing => AuthMember(standing, authTab)).ToArray();
        binding.Items["fleet"] = Lanes(team).ToArray();
        binding.Items["queue"] = input.Tasks.Select(Task).ToArray();
        binding.Items["devReview"] = (input.DevReviews ?? []).Select(Task).ToArray();
        binding.Items["pmReview"] = (input.PmReviews ?? []).Select(Task).ToArray();
        binding.Items["repos"] = (input.Repositories ?? []).Select(Repository).ToArray();
        var openRepo = (input.Repositories ?? []).FirstOrDefault(repo =>
            string.Equals(repo.Name, input.OpenRepository, StringComparison.OrdinalIgnoreCase));
        binding.Items["access"] = (openRepo?.Members ?? []).Select(Avatar).ToArray();
        binding.Text["repository"] = input.OpenRepository ?? "";
        binding.Text["forgeDetail"] = input.ForgeDetail;
        binding.Text["forgeEndpoint"] = input.ForgeEndpoint;
        binding.State["forgeTab"] = input.ForgeTab;
        if (input.ForgeConnected)
            binding.State["forgeConnected"] = "1";
        if (authTab.Length > 0)
        {
            binding.State["authTab"] = authTab;
            binding.State["authMember"] = "1";
        }
        binding.State["memberKind"] = input.MemberKind.Length > 0 ? input.MemberKind : "Author";
        binding.Text["agentEndpoint"] = input.AgentEndpoint;
        binding.Text["agentModel"] = input.AgentModel;
        if (string.Equals(input.BoundAgent, "opencode", StringComparison.OrdinalIgnoreCase))
            binding.State["opencodeSettings"] = "1";
        if (string.Equals(input.BoundAgent, "ollama", StringComparison.OrdinalIgnoreCase))
            binding.State["ollamaSettings"] = "1";
        if (string.IsNullOrEmpty(input.OpenRepository))
            binding.State["repoList"] = "1";
        else
            binding.State["repoOpen"] = "1";
        // The Tasks board shows one of its three queues; the scene marks the tab and the page.
        binding.State["tasksTab"] = input.TasksTab;
        binding.State[$"{input.TasksTab}Tab"] = "1";
        binding.Items["flows"] = input.Flows.Select(Flow).ToArray();
        binding.Items["chatFields"] = (input.ChatSettings ?? []).Select(Setting).ToArray();
        binding.Items["modules"] = (input.Modules ?? []).Select(Module).ToArray();
        // The open workspace's own applications. The scene marks the one the app state names.
        binding.Items["applications"] = (input.Applications ?? []).Select(application => new NewUiSlotItem
        {
            Id = application.Name,
            Parts = new HashSet<string>(StringComparer.Ordinal) { application.Running ? "running" : "stopped" },
            Text = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = application.Name },
        }).ToArray();
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

    private static NewUiSlotItem AuthMember(TeamStanding standing, string selectedId) => new()
    {
        Id = standing.WorkspaceId,
        State = standing.WorkspaceId == selectedId ? "au-app-tab--selected" : null,
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = standing.DisplayName,
        },
    };

    private static NewUiSlotItem Repository(CommandCenterRepositorySlot repository) => new()
    {
        Id = repository.Id,
        Action = "open-repo",
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = repository.Name,
            ["host"] = repository.Host,
        },
        Items = new Dictionary<string, List<NewUiSlotItem>>(StringComparer.Ordinal)
        {
            ["access"] = (repository.Members ?? []).Select(Avatar).ToList(),
        },
    };

    private static NewUiSlotItem Avatar(CommandCenterMemberSlot member) => new()
    {
        Id = member.Id,
        Text = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["initials"] = member.Initials,
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

    /// <summary>One row as the Server holds it. The scene groups, labels, and tones the board.</summary>
    private static NewUiSlotItem Task(CommandCenterTaskSlot task)
    {
        var parts = new HashSet<string>(StringComparer.Ordinal);
        if (task.Agent.Length > 0) parts.Add("agent");
        if (task.Initials.Length > 0) parts.Add("assignee");
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
                ["initials"] = task.Initials,
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
        var on = settings.CanCancelLoop;
        var autonomy = new CommandCenterSettingSlot(
            "autonomy",
            "Autonomous",
            on ? "On" : "Off",
            on ? "Disable" : "Enable",
            on ? "cancel-loop" : "toggle-autonomy",
            true);
        var agents = settings.Agents.Select(agent => AgentRow(agent, settings.BoundAgent)).ToArray();
        return agents.Length == 0 ? [autonomy] : [autonomy, .. agents];
    }

    private static CommandCenterSettingSlot AgentRow(AgentDescriptorDto agent, string? boundAgent)
    {
        var bound = string.Equals(agent.Agent, boundAgent, StringComparison.Ordinal);
        if (agent.Available)
            return new(agent.Agent, agent.DisplayName, bound ? "Bound" : agent.StatusLabel, bound ? "Bound" : "Use", "select-agent", !bound);
        if (agent.CanEnable)
            return new(agent.Agent, agent.DisplayName, agent.Remedy ?? agent.StatusLabel, "Enable", "enable-agent", true);
        return new(agent.Agent, agent.DisplayName, agent.Remedy ?? agent.StatusLabel, "Enable", null, false);
    }

    /// <summary>Which state a capability module is in; the scene words it and picks the button's tone.</summary>
    private static NewUiSlotItem Module(CommandCenterModuleSlot module) => new()
    {
        Id = module.Id,
        Action = module.Enabled ? "disable-module" : module.Available ? "enable-module" : "module-unavailable",
        Parts = new HashSet<string>(StringComparer.Ordinal)
        {
            !module.Available || (module.Enabled && !module.CanRun) ? "blocked" : module.Enabled ? "ready" : "disabled"
        },
        Text = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = module.DisplayName, ["why"] = module.State },
    };

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
        "agent-files" or "agent-file" => "3",
        "agent-settings" or "mobile-agent-settings" => "4",
        "application" or "mobile-application" => "5",
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

    public const string GithubDotCom = "https://github.com";

    /// <summary>
    /// The host URL the forge field should show: the saved origin, or github.com when this tab is
    /// GitHub and nothing is saved yet. Forgejo has no default.
    /// </summary>
    public static string PrefillEndpoint(string moduleId, string? saved)
    {
        if (!string.IsNullOrWhiteSpace(saved))
            return saved.Trim();
        return moduleId.Equals("github", StringComparison.OrdinalIgnoreCase) ? GithubDotCom : "";
    }

    /// <summary>
    /// Token and host URL the Save button should send. The scene keeps one field per action, so
    /// Save reads every field rather than only the one named save-token.
    /// </summary>
    public static (string Token, string EndpointUrl) TokenSave(
        IReadOnlyDictionary<string, string>? fields,
        string? text)
    {
        var token = Read(fields, "save-token") ?? text ?? "";
        var endpoint = Read(fields, "forge-endpoint") ?? "";
        return (token.Trim(), endpoint.Trim());
    }

    /// <summary>OpenCode and the in-process PM agent save an endpoint, optional token, and model.</summary>
    public static (string Endpoint, string Model, string Token) AgentSettingSave(
        IReadOnlyDictionary<string, string>? fields,
        string? text)
    {
        var endpoint = Read(fields, "agent-endpoint") ?? "";
        var model = Read(fields, "agent-model") ?? "";
        var token = Read(fields, "save-agent-settings") ?? text ?? "";
        return (endpoint.Trim(), model.Trim(), token.Trim());
    }

    /// <summary>
    /// The Tasks page names a repository as <c>module/owner/name</c>. Anything else is not a
    /// repository the page can open.
    /// </summary>
    public static (string ModuleId, string Owner, string Name)? ParseRepositoryKey(string? id)
    {
        var parts = id?.Split('/', 3);
        return parts is { Length: 3 } && parts.All(part => part.Length > 0)
            ? (parts[0], parts[1], parts[2])
            : null;
    }

    /// <summary>
    /// One row per review of the given kind, worded for the Dev review or PM review board.
    /// </summary>
    public static IReadOnlyList<CommandCenterTaskSlot> ReviewRows(
        IReadOnlyList<TaskSourcePull> pulls,
        string kind,
        string repo)
        => pulls.SelectMany(pull => pull.Reviews.Where(review => review.Kind == kind).Select(review => new CommandCenterTaskSlot(
            $"{pull.Id}:{kind}",
            $"#{pull.Number} · {pull.Title}",
            repo,
            review.Standing switch
            {
                "reviewed" => $"{review.Verdict} by {review.Login}",
                "reviewing" => review.Login ?? "",
                _ => "Waiting for a reviewer",
            },
            review.Standing))).ToArray();

    private static string? Read(IReadOnlyDictionary<string, string>? fields, string key)
        => fields is not null && fields.TryGetValue(key, out var value) ? value : null;
}
