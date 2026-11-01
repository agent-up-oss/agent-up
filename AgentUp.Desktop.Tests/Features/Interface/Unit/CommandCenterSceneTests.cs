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
            Assert.That(binding.Text["summary"], Is.EqualTo("No check selected"));
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
