using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

public sealed class CommandCenterSceneTests
{
    [Test]
    public void The_fleet_headline_and_lanes_are_the_catalog_copy()
    {
        var binding = CommandCenterScene.Bind(Input(
        [
            TeamStandingComposition.Standing(kind: TeamStandingKind.Working, displayName: "Bob", workspaceId: "bob", branch: "feat/45-deliver"),
            TeamStandingComposition.Standing(),
        ]));

        var lane = binding.Items["fleet"].Single(item => item.State == "aun-state-map__branch--waiting");

        Assert.Multiple(() =>
        {
            Assert.That(binding.Text["headline"], Is.EqualTo("2 agents · 1 working · 1 waiting for approval"));
            Assert.That(lane.Text["title"], Is.EqualTo("Wait for approval"));
            Assert.That(lane.Text["meta"], Is.EqualTo("Needs your decision"));
            Assert.That(lane.Items["hosts"].Single().Text["name"], Is.EqualTo("Lucy"));
            Assert.That(binding.Items["workspaces"].Single(item => item.Id == "bob").Parts, Does.Contain("working"));
            Assert.That(binding.Items["workspaces"].Single(item => item.Id == "bob").Parts, Does.Contain("branch"));
        });
    }

    [Test]
    public void An_empty_lane_is_omitted_and_a_missing_branch_drops_the_branch_line()
    {
        var standing = TeamStandingComposition.Standing(kind: TeamStandingKind.Idle, branch: "");
        var binding = CommandCenterScene.Bind(Input([standing]));

        Assert.Multiple(() =>
        {
            Assert.That(binding.Items["fleet"].Select(item => item.State), Is.EqualTo(new[] { "aun-state-map__branch--idle" }));
            Assert.That(binding.Items["workspaces"].Single().Parts, Does.Not.Contain("branch"));
            Assert.That(binding.Items["workspaces"].Single().Parts, Does.Contain("idle"));
            Assert.That(binding.State.ContainsKey("checks"), Is.False);
        });
    }

    [Test]
    public void Task_rows_name_the_catalog_action_and_the_dock_reports_a_run()
    {
        var binding = CommandCenterScene.Bind(Input([],
            tasks:
            [
                new CommandCenterTaskSlot("task-1", "#45 · Deliver", "shop/store", "Bob", "dequeue"),
            ],
            flows:
            [
                new CommandCenterFlowSlot("checkout", "Checkout featured goods", "Starting page"),
            ],
            validationsOpen: true,
            validationRunning: true));

        Assert.Multiple(() =>
        {
            Assert.That(binding.Items["queue"].Single().Action, Is.EqualTo("dequeue"));
            Assert.That(binding.Items["queue"].Single().Text["agent"], Is.EqualTo("Bob"));
            Assert.That(binding.Items["queue"].Single().Parts, Does.Not.Contain("assignee"));
            Assert.That(binding.Items["flows"].Single().Text["name"], Is.EqualTo("Checkout featured goods"));
            Assert.That(binding.State.ContainsKey("open"), Is.True);
            Assert.That(binding.State.ContainsKey("running"), Is.True);
            Assert.That(binding.State.ContainsKey("checks"), Is.True);
            Assert.That(binding.Text["summary"], Is.EqualTo("No check selected"));
            Assert.That(binding.Text["address"], Is.EqualTo(""));
        });
    }

    [Test]
    public void A_capability_whose_software_was_not_found_offers_no_enable_and_says_why()
    {
        var binding = CommandCenterScene.Bind(Input([], "settings") with
        {
            Modules =
            [
                new CommandCenterModuleSlot("dotnet", ".NET", Enabled: false, CanRun: false, "Needs Nix", Available: false),
                new CommandCenterModuleSlot("docker", "Docker", Enabled: false, CanRun: false, "Off")
            ]
        });

        var modules = binding.Items["modules"];
        Assert.That(modules.Select(item => item.Action), Is.EqualTo(new[] { "module-unavailable", "enable-module" }));
        Assert.That(modules[0].Parts, Is.EquivalentTo(new[] { "blocked" }));
        Assert.That(modules[0].Text["why"], Is.EqualTo("Needs Nix"));
    }

    [Test]
    public void The_application_tabs_are_the_open_workspaces_own_and_the_one_on_screen_is_selected()
    {
        var binding = CommandCenterScene.Bind(Input([], "application") with
        {
            SelectedApplication = "Hello",
            Applications = [new CommandCenterApplicationSlot("Hello", true), new CommandCenterApplicationSlot("Worker", false)],
        });

        var tabs = binding.Items["applications"];

        Assert.Multiple(() =>
        {
            Assert.That(tabs.Select(tab => tab.Text["name"]), Is.EqualTo(new[] { "Hello", "Worker" }));
            Assert.That(binding.State["app"], Is.EqualTo("Hello"));
            Assert.That(tabs.Select(tab => tab.Parts.Single()), Is.EqualTo(new[] { "running", "stopped" }));
        });
    }

    [Test]
    public void The_console_surface_names_its_tool_and_a_flow_row_keeps_its_id()
    {
        var binding = CommandCenterScene.Bind(Input([],
            flows:
            [
                new CommandCenterFlowSlot("checkout", "Checkout featured goods", "Starting page"),
            ]) with
        {
            PortLabel = "3000:9100",
            Tool = "console",
            Browser = false,
            Tools = ["port", "console"],
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State["tool"], Is.EqualTo("console"));
            Assert.That(binding.Text["port"], Is.EqualTo("3000:9100"));
            Assert.That(binding.State.ContainsKey("browser"), Is.False);
            Assert.That(binding.State.ContainsKey("console"), Is.True);
            Assert.That(binding.State.ContainsKey("diagnostics"), Is.False);
            Assert.That(binding.Items["flows"].Single().Id, Is.EqualTo("checkout"));
            Assert.That(binding.Items["flows"].Single().Action, Is.Null);
        });
    }

    [Test]
    public void The_open_application_is_the_tab_that_carries_the_accent()
    {
        var binding = CommandCenterScene.Bind(Input([], "application") with { SelectedApplication = "Orders API" });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State["selected"], Is.EqualTo("5"));
            Assert.That(binding.State["app"], Is.EqualTo("Orders API"));
        });
    }

    [TestCase("agent-files")]
    [TestCase("agent-file")]
    public void Files_and_an_open_file_select_the_Files_tab(string scene)
        => Assert.That(CommandCenterScene.Bind(Input([], scene)).State["selected"], Is.EqualTo("3"));

    [Test]
    public void A_validation_card_keeps_the_flow_stages_and_checks()
    {
        var binding = CommandCenterScene.Bind(Input([],
            flows:
            [
                new CommandCenterFlowSlot(
                    "pack",
                    "Pack the open ticket",
                    "Pack the seeded open mug ticket, then ship it.",
                    "pending",
                    [
                        new CommandCenterStageSlot(
                            "start",
                            "Starting page",
                            "pending",
                            [new CommandCenterCheckSlot("Title is Harbor Shop Orders", "pending")]),
                    ]),
            ]));
        var flow = binding.Items["flows"].Single();
        var stage = flow.Items["stages"].Single();

        Assert.Multiple(() =>
        {
            Assert.That(flow.State, Is.EqualTo("au-status-dot--idle"));
            Assert.That(flow.Text["description"], Is.EqualTo("Pack the seeded open mug ticket, then ship it."));
            Assert.That(flow.Parts, Does.Contain("play"));
            Assert.That(stage.Text["label"], Is.EqualTo("Starting page"));
            Assert.That(stage.Items["checks"].Single().Text["label"], Is.EqualTo("Title is Harbor Shop Orders"));
        });
    }

    [Test]
    public void One_assignee_is_the_round_initials_on_the_task_row()
    {
        var binding = CommandCenterScene.Bind(Input([],
            tasks: [new CommandCenterTaskSlot("task-1", "#45 · Deliver", "shop/store", "Bob", "dequeue", "BO")]));

        Assert.Multiple(() =>
        {
            Assert.That(binding.Items["queue"].Single().Parts, Does.Contain("assignee"));
            Assert.That(binding.Items["queue"].Single().Text["initials"], Is.EqualTo("BO"));
        });
    }

    [Test]
    public void Auth_tabs_are_one_per_team_member_and_the_open_repository_names_who_can_see_it()
    {
        var binding = CommandCenterScene.Bind(Input(
        [
            TeamStandingComposition.Standing(displayName: "Bob", workspaceId: "bob"),
            TeamStandingComposition.Standing(displayName: "Frank", workspaceId: "frank"),
        ], sceneId: "settings") with
        {
            AuthTab = "frank",
            OpenRepository = "example/repository",
            Repositories =
            [
                new CommandCenterRepositorySlot(
                    "github/example/repository",
                    "example/repository",
                    "GitHub",
                    [new CommandCenterMemberSlot("bob", "Bob", "BO"), new CommandCenterMemberSlot("frank", "Frank", "FR")]),
            ],
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.Items["authMembers"].Select(item => item.Text["name"]), Is.EqualTo(new[] { "Bob", "Frank" }));
            Assert.That(binding.Items["authMembers"].Single(item => item.Id == "frank").State, Is.EqualTo("au-app-tab--selected"));
            Assert.That(binding.State["authTab"], Is.EqualTo("frank"));
            Assert.That(binding.Items["access"].Select(item => item.Text["initials"]), Is.EqualTo(new[] { "BO", "FR" }));
            Assert.That(binding.Items["repos"].Single().Items["access"].Select(item => item.Text["initials"]), Is.EqualTo(new[] { "BO", "FR" }));
        });
    }

    [Test]
    public void Settings_names_the_workspace_whose_chat_is_being_set()
    {
        var binding = CommandCenterScene.Bind(Input(
        [
            TeamStandingComposition.Standing(displayName: "Bob", workspaceId: "bob"),
            TeamStandingComposition.Standing(displayName: "Frank", workspaceId: "frank"),
        ]) with
        {
            ValidationCollapsed = false,
            SettingsWorkspaceId = "frank",
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State.ContainsKey("expanded"), Is.True);
            Assert.That(binding.State.ContainsKey("collapsed"), Is.False);
            Assert.That(binding.Text["validationToggle"], Is.EqualTo("›"));
            Assert.That(binding.Items["settingsWorkspaces"].Single(item => item.Id == "frank").State, Is.EqualTo("au-app-tab--selected"));
            Assert.That(binding.Items["settingsWorkspaces"].Single(item => item.Id == "bob").Text["name"], Is.EqualTo("Bob"));
        });
    }

    [Test]
    public void Member_settings_are_the_autonomous_toggle_and_the_agents()
    {
        var fields = CommandCenterScene.ChatFields(new CommandCenterChatSettings(
            [
                new AutonomyPreconditionDto("forgeCapabilityEnabled", true),
                new AutonomyPreconditionDto("autonomyEnabled", false, "Enable autonomy for this Server"),
                new AutonomyPreconditionDto("agentAvailable", true),
                new AutonomyPreconditionDto("boundAgent", false, "Bind an agent"),
            ],
            [
                new AgentDescriptorDtoBuilder().Named("codex", "Codex").Available().Build(),
                new AgentDescriptorDtoBuilder().Named("cursor", "Cursor").PackageDisabled().Build(),
                new AgentDescriptorDtoBuilder().Named("claude", "Claude").ExecutableMissing().Build(),
            ],
            BoundAgent: "codex",
            LoopDetail: null,
            CanEnableAutonomy: true,
            CanActivateLoop: false,
            CanCancelLoop: false,
            CanStartBound: false));
        var rows = CommandCenterScene.Bind(Input([]) with { ChatSettings = fields }).Items["chatFields"];

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(item => item.Id), Is.EqualTo(new[] { "autonomy", "codex", "cursor", "claude" }));
            Assert.That(rows.Single(item => item.Id == "autonomy").Action, Is.EqualTo("toggle-autonomy"));
            Assert.That(rows.Single(item => item.Id == "autonomy").Text["detail"], Is.EqualTo("Off"));
            Assert.That(rows.Single(item => item.Id == "codex").Text["detail"], Is.EqualTo("Bound"));
            Assert.That(rows.Single(item => item.Id == "cursor").Action, Is.EqualTo("enable-agent"));
            Assert.That(rows.Single(item => item.Id == "claude").Parts, Does.Contain("locked"));
            Assert.That(rows.Any(item => item.Id == "forgeCapabilityEnabled"), Is.False);
        });
    }

    [Test]
    public void Member_settings_mark_kind_and_OpenCode_fields()
    {
        var binding = CommandCenterScene.Bind(Input([], "agent-settings") with
        {
            MemberKind = "DevReviewer",
            BoundAgent = "opencode",
            AgentEndpoint = "http://localhost:11434",
            AgentModel = "llama",
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State["selected"], Is.EqualTo("4"));
            Assert.That(binding.State["memberKind"], Is.EqualTo("DevReviewer"));
            Assert.That(binding.State["opencodeSettings"], Is.EqualTo("1"));
            Assert.That(binding.Text["agentEndpoint"], Is.EqualTo("http://localhost:11434"));
            Assert.That(binding.Text["agentModel"], Is.EqualTo("llama"));
        });
    }

    [Test]
    public void Member_settings_bind_prompts_and_the_overwrite_warning()
    {
        var binding = CommandCenterScene.Bind(Input([], "agent-settings") with
        {
            SystemPrompt = "Ship the cards.",
            CustomizationPrompt = "Keep the comma.",
            PromptOverwrite = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.Text["systemPrompt"], Is.EqualTo("Ship the cards."));
            Assert.That(binding.Text["customizationPrompt"], Is.EqualTo("Keep the comma."));
            Assert.That(binding.State["promptOverwrite"], Is.EqualTo("1"));
            Assert.That(CommandCenterScene.SystemPromptWasCustomized("Ship the cards.", "Edit the files."), Is.True);
            Assert.That(CommandCenterScene.SystemPromptWasCustomized("Edit the files.", "Edit the files."), Is.False);
            Assert.That(CommandCenterTaskSourceBoard.RefreshSeconds, Is.EqualTo(30));
        });
    }

    [Test]
    public void Member_settings_show_Ollama_fields_for_the_in_process_PM_agent()
    {
        var binding = CommandCenterScene.Bind(Input([], "agent-settings") with { BoundAgent = "ollama" });

        Assert.That(binding.State["ollamaSettings"], Is.EqualTo("1"));
    }

    // The board opens one queue; a review row carries its standing as the action the scene groups by.
    [Test]
    public void The_tasks_board_opens_one_queue_and_lists_each_kind_of_review()
    {
        var binding = CommandCenterScene.Bind(Input([], sceneId: "tasks") with
        {
            DevReviews = [new CommandCenterTaskSlot("I-39", "#39 · Product cards", "example/repository", "Hector · revision 2", "reviewing")],
            TasksTab = "dev",
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State["tasksTab"], Is.EqualTo("dev"));
            Assert.That(binding.State["devTab"], Is.EqualTo("1"));
            Assert.That(binding.State["repoList"], Is.EqualTo("1"));
            Assert.That(binding.State.ContainsKey("issuesTab"), Is.False);
            Assert.That(binding.Items["devReview"].Single().Action, Is.EqualTo("reviewing"));
            Assert.That(binding.Items["pmReview"], Is.Empty);
        });
    }

    [Test]
    public void An_open_repository_replaces_the_repository_list()
    {
        var binding = CommandCenterScene.Bind(Input([], sceneId: "tasks") with
        {
            OpenRepository = "example/repository",
            Repositories = [new CommandCenterRepositorySlot("github/example/repository", "example/repository", "GitHub")],
            ForgeTab = "forgejo",
            ForgeConnected = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(binding.State["repoOpen"], Is.EqualTo("1"));
            Assert.That(binding.State.ContainsKey("repoList"), Is.False);
            Assert.That(binding.Text["repository"], Is.EqualTo("example/repository"));
            Assert.That(binding.Items["repos"].Single().Text["host"], Is.EqualTo("GitHub"));
            Assert.That(binding.State["forgeConnected"], Is.EqualTo("1"));
            Assert.That(binding.State["forgeTab"], Is.EqualTo("forgejo"));
        });
    }

    [Test]
    public void GitHubPrefillsGithubDotComAndForgejoHasNoDefaultHost()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommandCenterScene.PrefillEndpoint("github", null), Is.EqualTo("https://github.com"));
            Assert.That(CommandCenterScene.PrefillEndpoint("forgejo", null), Is.EqualTo(""));
            Assert.That(CommandCenterScene.PrefillEndpoint("github", " https://ghe.example.test "), Is.EqualTo("https://ghe.example.test"));
        });
    }

    [Test]
    public void SaveReadsTheHostUrlAndTokenFromEveryField()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["forge-endpoint"] = " https://git.example.test ",
            ["save-token"] = " secret ",
        };

        var saved = CommandCenterScene.TokenSave(fields, "ignored");
        var fromText = CommandCenterScene.TokenSave(null, " pasted ");

        Assert.Multiple(() =>
        {
            Assert.That(saved.Token, Is.EqualTo("secret"));
            Assert.That(saved.EndpointUrl, Is.EqualTo("https://git.example.test"));
            Assert.That(fromText.Token, Is.EqualTo("pasted"));
            Assert.That(fromText.EndpointUrl, Is.EqualTo(""));
        });
    }

    [Test]
    public void AgentSettingSave_readsEndpointModelAndToken()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agent-endpoint"] = " http://localhost:11434 ",
            ["agent-model"] = " llama ",
            ["save-agent-settings"] = " secret ",
        };

        var saved = CommandCenterScene.AgentSettingSave(fields, "ignored");
        var fromText = CommandCenterScene.AgentSettingSave(null, " pasted ");

        Assert.Multiple(() =>
        {
            Assert.That(saved.Endpoint, Is.EqualTo("http://localhost:11434"));
            Assert.That(saved.Model, Is.EqualTo("llama"));
            Assert.That(saved.Token, Is.EqualTo("secret"));
            Assert.That(fromText.Token, Is.EqualTo("pasted"));
            Assert.That(fromText.Endpoint, Is.EqualTo(""));
        });
    }

    [Test]
    public void ParseRepositoryKey_readsModuleOwnerAndName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommandCenterScene.ParseRepositoryKey("github/example/repository"), Is.EqualTo(("github", "example", "repository")));
            Assert.That(CommandCenterScene.ParseRepositoryKey("github/example"), Is.Null);
            Assert.That(CommandCenterScene.ParseRepositoryKey(""), Is.Null);
        });
    }

    [Test]
    public void ReviewRows_wordTheStandingTheBoardShows()
    {
        var pulls = new[]
        {
            new TaskSourcePull("42", 42, "Product cards", "u",
            [
                new TaskSourceReview("devReviewer", "reviewing", "hector", null),
                new TaskSourceReview("pmReviewer", "reviewed", "lucy", "Approved"),
                new TaskSourceReview("devReviewer", "waiting", null, null),
            ]),
        };

        var reviewing = CommandCenterScene.ReviewRows(pulls, "devReviewer", "example/repository");
        var reviewed = CommandCenterScene.ReviewRows(pulls, "pmReviewer", "example/repository");

        Assert.Multiple(() =>
        {
            Assert.That(reviewing.Select(row => row.Agent), Is.EqualTo(new[] { "hector", "Waiting for a reviewer" }));
            Assert.That(reviewed.Single().Agent, Is.EqualTo("Approved by lucy"));
            Assert.That(reviewed.Single().Action, Is.EqualTo("reviewed"));
        });
    }

    [Test]
    public void The_forge_field_shows_the_host_url()
    {
        var binding = CommandCenterScene.Bind(Input([], sceneId: "agent-settings") with
        {
            ForgeTab = "github",
            ForgeEndpoint = "https://github.com",
        });

        Assert.That(binding.Text["forgeEndpoint"], Is.EqualTo("https://github.com"));
    }

    private static CommandCenterSceneInput Input(
        IReadOnlyList<TeamStanding> team,
        string sceneId = "team",
        IReadOnlyList<CommandCenterTaskSlot>? tasks = null,
        IReadOnlyList<CommandCenterFlowSlot>? flows = null,
        bool validationsOpen = false,
        bool validationRunning = false)
        => new(
            sceneId,
            "http://localhost:5000",
            team.FirstOrDefault()?.WorkspaceId,
            validationsOpen,
            validationRunning,
            ValidationPassed: false,
            "No check selected",
            team,
            tasks ?? [],
            flows ?? []);
}
