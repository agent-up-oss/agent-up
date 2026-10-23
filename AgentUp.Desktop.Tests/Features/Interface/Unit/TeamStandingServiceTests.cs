using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class TeamStandingServiceTests
{
    [Test]
    public async Task ReadAsync_classifiesARunningLoopAsWorking()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Sessions["shop"] = TeamStandingComposition.Session(
            "shop",
            loop: TeamStandingComposition.Loop(true, "running", 44, "Restock featured products", "Running"));
        forge.Hosting["shop"] = TeamStandingComposition.Hosting(
            checks: [TeamStandingComposition.Check("build", "completed", "success")]);

        var standing = (await Read(agents, forge, "shop")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.Working));
            Assert.That(standing.TaskNumber, Is.EqualTo(44));
            Assert.That(standing.TaskLabel, Is.EqualTo("#44 · Restock featured products"));
            Assert.That(standing.Forge.ChecksPassed, Is.EqualTo(1));
            Assert.That(standing.Forge.HasProposal, Is.True);
        });
    }

    [Test]
    public async Task ReadAsync_classifiesABlockedLoopAsWaitingOnYou()
    {
        var agents = new StandingAgentFake();
        agents.Sessions["checkout"] = TeamStandingComposition.Session(
            "checkout",
            state: "awaiting_input",
            loop: TeamStandingComposition.Loop(
                true,
                "blocked",
                48,
                "Express checkout",
                "BlockedAwaitingInput",
                blockReason: "Waiting for you to approve the review before it is posted.",
                permissionRuleIds: ["forge.submit_review"]));

        var standing = (await Read(agents, new StandingForgeFake(), "checkout")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.WaitingOnYou));
            Assert.That(standing.Reason, Does.Contain("approve the review"));
            Assert.That(standing.PermissionRuleIds, Is.EqualTo(new[] { "forge.submit_review" }));
        });
    }

    [Test]
    public async Task ReadAsync_classifiesAFailedLoopAsHaltedAndKeepsTheDiagnostic()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Sessions["docs"] = TeamStandingComposition.Session(
            "docs",
            state: "error",
            loop: TeamStandingComposition.Loop(false, "failed", 51, "Docs anchors", "Failed", stopReason: "forge.unreachable"));
        forge.Hosting["docs"] = TeamStandingComposition.Hosting(
            availability: "unreachable",
            number: null,
            mergeState: null,
            detail: "The repository host returned 502 on read.");

        var standing = (await Read(agents, forge, "docs")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.Halted));
            Assert.That(standing.DiagnosticCode, Is.EqualTo("forge.unreachable"));
            Assert.That(standing.Reason, Does.Contain("502"));
            Assert.That(standing.Forge.HasProposal, Is.False);
        });
    }

    [Test]
    public async Task ReadAsync_anEmptyQueueIsIdleRatherThanHalted()
    {
        var agents = new StandingAgentFake();
        agents.Sessions["idle"] = TeamStandingComposition.Session(
            "idle",
            loop: TeamStandingComposition.Loop(false, "idle", stopReason: "queue_empty"));

        var standing = (await Read(agents, new StandingForgeFake(), "idle")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.Idle));
            Assert.That(standing.TaskLabel, Is.Null);
        });
    }

    [Test]
    public async Task ReadAsync_countsChecksAndUnresolvedThreads()
    {
        var forge = new StandingForgeFake();
        forge.Hosting["checkout"] = TeamStandingComposition.Hosting(
            checks:
            [
                TeamStandingComposition.Check("build", "completed", "success"),
                TeamStandingComposition.Check("e2e", "completed", "failure"),
                TeamStandingComposition.Check("lint", "in_progress", string.Empty)
            ],
            threads:
            [
                TeamStandingComposition.Thread("t1", resolved: true),
                TeamStandingComposition.Thread("t2", resolved: false),
                TeamStandingComposition.Thread("t3", resolved: false)
            ],
            mergeState: "unstable");

        var standing = (await Read(new StandingAgentFake(), forge, "checkout")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Forge.ChecksPassed, Is.EqualTo(1));
            Assert.That(standing.Forge.ChecksFailed, Is.EqualTo(1));
            Assert.That(standing.Forge.ChecksRunning, Is.EqualTo(1));
            Assert.That(standing.Forge.UnresolvedThreads, Is.EqualTo(2));
            Assert.That(standing.Forge.MergeState, Is.EqualTo("unstable"));
        });
    }

    [Test]
    public async Task ReadAsync_aWorkspaceWithoutEitherReadStillStands()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Failures["blind"] = new HttpRequestException("agent read refused");
        forge.Failures["blind"] = new HttpRequestException("forge read refused");

        var standing = (await Read(agents, forge, "blind")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.Idle));
            Assert.That(standing.Agent, Is.Null);
            Assert.That(standing.Forge.Availability, Is.EqualTo("none"));
            Assert.That(standing.DisplayName, Is.EqualTo("blind"));
        });
    }

    private static Task<IReadOnlyList<TeamStanding>> Read(
        StandingAgentFake agents,
        StandingForgeFake forge,
        params string[] workspaceIds)
        => TeamStandingComposition.Controller(agents, forge).ReadAsync(
            workspaceIds.Select(id => new TeamMember(id, id, "main", "Running")).ToArray());
}
