using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class BridgeViewModelTests
{
    [Test]
    public async Task ReadAsync_groupsTheTeamByWhatItNeedsFromYou()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Sessions["shop"] = TeamStandingComposition.Session(
            "shop",
            loop: TeamStandingComposition.Loop(true, "running", 44, "Restock featured products", "Running"));
        agents.Sessions["checkout"] = TeamStandingComposition.Session(
            "checkout",
            state: "awaiting_input",
            loop: TeamStandingComposition.Loop(
                true, "blocked", 48, "Express checkout", "BlockedAwaitingInput",
                blockReason: "Waiting for you to approve the review before it is posted.",
                permissionRuleIds: ["forge.submit_review"]));
        agents.Sessions["docs"] = TeamStandingComposition.Session(
            "docs",
            state: "error",
            loop: TeamStandingComposition.Loop(false, "failed", 51, "Docs anchors", "Failed", stopReason: "forge.unreachable"));
        var bridge = new BridgeViewModel(TeamStandingComposition.Controller(agents, forge));

        await bridge.ReadAsync(Members("shop", "checkout", "docs"));

        Assert.Multiple(() =>
        {
            Assert.That(bridge.Team, Has.Count.EqualTo(3));
            Assert.That(bridge.WaitingOnYou.Select(row => row.WorkspaceId), Is.EqualTo(new[] { "checkout" }));
            Assert.That(bridge.Halted.Select(row => row.WorkspaceId), Is.EqualTo(new[] { "docs" }));
            Assert.That(bridge.Tasks, Has.Count.EqualTo(3));
            Assert.That(bridge.Headline, Is.EqualTo("3 agents · 1 waiting on you · 1 halted"));
            Assert.That(bridge.HasRead, Is.True);
            Assert.That(bridge.IsLoading, Is.False);
        });
    }

    [Test]
    public async Task ReadAsync_anEmptyServerSaysSoRatherThanLookingUnread()
    {
        var bridge = new BridgeViewModel(
            TeamStandingComposition.Controller(new StandingAgentFake(), new StandingForgeFake()));

        await bridge.ReadAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(bridge.HasTeam, Is.False);
            Assert.That(bridge.HasWaiting, Is.False);
            Assert.That(bridge.HasHalted, Is.False);
            Assert.That(bridge.HasTasks, Is.False);
            Assert.That(bridge.HasRead, Is.True);
            Assert.That(bridge.Headline, Is.EqualTo("0 agents · 0 waiting on you · 0 halted"));
            Assert.That(bridge.HasError, Is.False);
        });
    }

    [Test]
    public async Task ReadAsync_onlyListsTasksAnAgentReportsHolding()
    {
        var agents = new StandingAgentFake();
        agents.Sessions["shop"] = TeamStandingComposition.Session(
            "shop",
            loop: TeamStandingComposition.Loop(false, "idle", stopReason: "queue_empty"));
        var bridge = new BridgeViewModel(TeamStandingComposition.Controller(agents, new StandingForgeFake()));

        await bridge.ReadAsync(Members("shop"));

        Assert.Multiple(() =>
        {
            Assert.That(bridge.Team, Has.Count.EqualTo(1));
            Assert.That(bridge.Tasks, Is.Empty);
            Assert.That(bridge.TasksCaveat, Does.Contain("does not expose its queue"));
        });
    }

    [Test]
    public async Task ReadAsync_withNoMembersGivenRereadsTheOnesItHolds()
    {
        var agents = new StandingAgentFake();
        agents.Sessions["shop"] = TeamStandingComposition.Session(
            "shop",
            loop: TeamStandingComposition.Loop(true, "running", 44, "Restock featured products", "Running"));
        var bridge = new BridgeViewModel(TeamStandingComposition.Controller(agents, new StandingForgeFake()));
        await bridge.ReadAsync(Members("shop"));

        agents.Sessions["shop"] = TeamStandingComposition.Session(
            "shop",
            state: "awaiting_input",
            loop: TeamStandingComposition.Loop(
                true, "blocked", 44, "Restock featured products", "BlockedAwaitingInput",
                blockReason: "Waiting for you to approve the pull request."));
        await bridge.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(bridge.Team, Has.Count.EqualTo(1));
            Assert.That(bridge.WaitingOnYou, Has.Count.EqualTo(1));
            Assert.That(bridge.WaitingOnYou[0].Reason, Does.Contain("approve the pull request"));
        });
    }

    [Test]
    public async Task ReadAsync_theHeadlineCountsRatherThanStayingOnReading()
    {
        var bridge = new BridgeViewModel(
            TeamStandingComposition.Controller(new StandingAgentFake(), new StandingForgeFake()));
        var headlines = new List<string>();
        bridge.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(BridgeViewModel.Headline)) headlines.Add(bridge.Headline);
        };

        Assert.That(bridge.Headline, Is.EqualTo("Reading the team…"));

        await bridge.ReadAsync(Members("shop"));

        Assert.Multiple(() =>
        {
            Assert.That(bridge.Headline, Is.EqualTo("1 agent · 0 waiting on you · 0 halted"));
            Assert.That(headlines, Does.Not.Contain("Reading the team…"));
        });
    }

    private static TeamMember[] Members(params string[] ids)
        => ids.Select(id => new TeamMember(id, id, "main", "Running")).ToArray();
}

[TestFixture]
public sealed class StandingRowViewModelTests
{
    [Test]
    public void Row_namesTheGateTheStateAndWhatTheHostReports()
    {
        var row = new StandingRowViewModel(new TeamStanding(
            "checkout",
            "Harbor Checkout",
            "feat/48-express-checkout",
            "Running",
            "Claude",
            TeamStandingKind.WaitingOnYou,
            48,
            "Express checkout",
            "Waiting for you to approve the review before it is posted.",
            null,
            ["forge.submit_review"],
            new ForgeStanding("ready", "48", "Express checkout", "https://example/pull/48", 1, 1, 1, 2, "unstable", null)));

        Assert.Multiple(() =>
        {
            Assert.That(row.Initial, Is.EqualTo("H"));
            Assert.That(row.StateLabel, Is.EqualTo("◆ gate open"));
            Assert.That(row.Headline, Is.EqualTo("Harbor Checkout is waiting on you on #48"));
            Assert.That(row.PermissionSummary, Is.EqualTo("needs forge.submit_review"));
            Assert.That(row.ChecksSummary, Is.EqualTo("1 passing · 1 failing · 1 running"));
            Assert.That(row.ThreadSummary, Is.EqualTo("2 unresolved review threads"));
            Assert.That(row.ForgeStatus, Is.EqualTo("mergeable: unstable"));
            Assert.That(row.Subtitle, Is.EqualTo("Claude · Running"));
            Assert.That(row.HasProposal, Is.True);
            Assert.That(row.ShowProposalBesideTask, Is.False, "the proposal repeats task #48 word for word");
        });
    }

    [Test]
    public void Row_namesTheProposalBesideTheTaskOnlyWhenItIsADifferentNumber()
    {
        var standing = new TeamStanding(
            "shop", "Harbor Shop", "main", "Running", "Codex", TeamStandingKind.Working,
            44, "Restock", null, null, [],
            new ForgeStanding("ready", "52", "Restock, split out", "u", 0, 0, 0, 0, "clean", null));

        Assert.Multiple(() =>
        {
            Assert.That(new StandingRowViewModel(standing).ShowProposalBesideTask, Is.True);
            Assert.That(
                new StandingRowViewModel(standing with { Forge = standing.Forge with { ProposalNumber = "44" } })
                    .ShowProposalBesideTask,
                Is.False);
        });
    }

    [Test]
    public void Row_withNothingReportedSaysSoInsteadOfShowingZeroes()
    {
        var row = new StandingRowViewModel(new TeamStanding(
            "fresh", "Fresh", "main", "Stopped", null, TeamStandingKind.Idle,
            null, null, null, null, [], ForgeStanding.None));

        Assert.Multiple(() =>
        {
            Assert.That(row.StateLabel, Is.EqualTo("○ idle"));
            Assert.That(row.Headline, Is.EqualTo("Fresh has nothing in hand"));
            Assert.That(row.TaskLabel, Is.EqualTo("no task in hand"));
            Assert.That(row.ChecksSummary, Is.EqualTo("no checks reported"));
            Assert.That(row.ForgeStatus, Is.EqualTo("no host connected"));
            Assert.That(row.HasProposal, Is.False);
            Assert.That(row.HasUnresolvedThreads, Is.False);
            Assert.That(row.HasPermissionSummary, Is.False);
            Assert.That(row.Subtitle, Is.EqualTo("Stopped"));
        });
    }

    [Test]
    public void Row_namesAHaltedRunAndItsDiagnostic()
    {
        var row = new StandingRowViewModel(new TeamStanding(
            "docs", "Harbor Docs", "fix/51-broken-anchors", "Failed", "Claude", TeamStandingKind.Halted,
            51, "Docs anchors break on deep links",
            "The repository host returned 502 on read.", "forge.unreachable", [],
            new ForgeStanding("unreachable", null, null, null, 0, 0, 0, 0, null,
                "The repository host returned 502 on read.")));

        Assert.Multiple(() =>
        {
            Assert.That(row.StateLabel, Is.EqualTo("▲ halted"));
            Assert.That(row.Headline, Is.EqualTo("Harbor Docs stopped on #51"));
            Assert.That(row.HasDiagnosticCode, Is.True);
            Assert.That(row.DiagnosticCode, Is.EqualTo("forge.unreachable"));
            Assert.That(row.ForgeStatus, Is.EqualTo("host unreachable"));
            Assert.That(row.TaskLabel, Is.EqualTo("#51 · Docs anchors break on deep links"));
        });
    }
}
