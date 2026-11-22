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
            Assert.That(binding.State["selected"], Is.EqualTo("4"));
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
    public void Chat_settings_are_one_field_per_property_and_a_locked_control_cannot_run()
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
            BoundAgent: null,
            LoopDetail: null,
            CanEnableAutonomy: true,
            CanActivateLoop: false,
            CanCancelLoop: false,
            CanStartBound: false));
        var rows = CommandCenterScene.Bind(Input([]) with { ChatSettings = fields }).Items["chatFields"];
        var agents = rows.Single(item => item.Id == "agentAvailable").Items["fields"];

        Assert.Multiple(() =>
        {
            Assert.That(rows.Single(item => item.Id == "forgeCapabilityEnabled").Parts, Does.Contain("locked"));
            Assert.That(rows.Single(item => item.Id == "forgeCapabilityEnabled").Action, Is.Null);
            Assert.That(rows.Single(item => item.Id == "autonomyEnabled").Action, Is.EqualTo("enable-autonomy"));
            Assert.That(rows.Single(item => item.Id == "autonomyEnabled").Parts, Does.Not.Contain("locked"));
            Assert.That(rows.Single(item => item.Id == "autonomyEnabled").Text["detail"], Is.EqualTo("Enable autonomy for this Server"));
            Assert.That(rows.Single(item => item.Id == "autonomyEnabled").Items["fields"].Single(item => item.Id == "loop").Parts, Does.Contain("locked"));
            Assert.That(agents.Single(item => item.Id == "codex").Action, Is.EqualTo("select-agent"));
            Assert.That(agents.Single(item => item.Id == "cursor").Action, Is.EqualTo("enable-agent"));
            Assert.That(agents.Single(item => item.Id == "claude").Parts, Does.Contain("locked"));
            Assert.That(rows.Single(item => item.Id == "boundAgent").Parts, Does.Contain("locked"));
        });
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
            Assert.That(binding.State.ContainsKey("issuesTab"), Is.False);
            Assert.That(binding.Items["devReview"].Single().Action, Is.EqualTo("reviewing"));
            Assert.That(binding.Items["pmReview"], Is.Empty);
        });
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
