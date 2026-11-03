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
                new CommandCenterTaskSlot("task-1", "#45 · Deliver", "shop/store", "Bob", "dequeue", "Dequeue"),
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
            Assert.That(binding.Items["queue"].Single().Text["action"], Is.EqualTo("Dequeue"));
            Assert.That(binding.Items["flows"].Single().Text["name"], Is.EqualTo("Checkout featured goods"));
            Assert.That(binding.State.ContainsKey("open"), Is.True);
            Assert.That(binding.State.ContainsKey("running"), Is.True);
            Assert.That(binding.State.ContainsKey("checks"), Is.True);
            Assert.That(binding.Text["summary"], Is.EqualTo("No check selected"));
            Assert.That(binding.Text["address"], Is.EqualTo(""));
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
            Assert.That(binding.State["selected"], Is.EqualTo("3"));
            Assert.That(binding.State["app"], Is.EqualTo("Orders API"));
        });
    }

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
