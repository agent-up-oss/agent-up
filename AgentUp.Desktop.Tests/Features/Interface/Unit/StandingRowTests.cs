using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// How one agent reads as a row. A developer builds on their own branch and a reviewer reads
/// somebody else's, so the two never describe themselves the same way.
/// </summary>
[TestFixture]
public sealed class StandingRowTests
{
    [Test]
    public void Headline_saysADeveloperWorksOnTheirTask()
    {
        var row = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer));

        Assert.That(row.Headline, Is.EqualTo("Bob is working on #44"));
    }

    [Test]
    public void Headline_saysAReviewerReviewsIt()
    {
        var row = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Reviewer, name: "Lucy"));

        Assert.That(row.Headline, Is.EqualTo("Lucy is reviewing #44"),
            "a reviewer's branch is not theirs, so they never work on the issue");
    }

    [Test]
    public void Headline_keepsEachRolesVerbWhenTheRunStopped()
    {
        var developer = Row(Standing(TeamStandingKind.Halted, TeamRoleKind.Developer));
        var reviewer = Row(Standing(TeamStandingKind.Halted, TeamRoleKind.Reviewer, name: "Hector"));

        Assert.Multiple(() =>
        {
            Assert.That(developer.Headline, Is.EqualTo("Bob stopped working on #44"));
            Assert.That(reviewer.Headline, Is.EqualTo("Hector stopped reviewing #44"));
        });
    }

    [Test]
    public void Headline_readsTheSameAtAGateWhoeverHoldsIt()
    {
        var developer = Row(Standing(TeamStandingKind.WaitingOnYou, TeamRoleKind.Developer));
        var reviewer = Row(Standing(TeamStandingKind.WaitingOnYou, TeamRoleKind.Reviewer, name: "Lucy"));

        Assert.Multiple(() =>
        {
            Assert.That(developer.Headline, Is.EqualTo("Bob is waiting on you on #44"));
            Assert.That(reviewer.Headline, Is.EqualTo("Lucy is waiting on you on #44"),
                "what a gate needs is yours whoever is held at it");
        });
    }

    [Test]
    public void Headline_saysSoWhenAnAgentHoldsNothing()
    {
        var idle = Row(Standing(TeamStandingKind.Idle, TeamRoleKind.Developer));
        var working = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, task: null));

        Assert.Multiple(() =>
        {
            Assert.That(idle.Headline, Is.EqualTo("Bob has nothing in hand"));
            Assert.That(working.Headline, Is.EqualTo("Bob is working"), "no task, so no preposition");
            Assert.That(working.TaskLabel, Is.EqualTo("no task in hand"));
            Assert.That(working.HasTask, Is.False);
        });
    }

    [Test]
    public void HasStatusRow_staysWithTheAgentWhoseBranchTheProposalIsOn()
    {
        var author = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal()));
        var reviewer = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Reviewer, forge: Proposal()));

        Assert.Multiple(() =>
        {
            Assert.That(author.HasStatusRow, Is.True);
            Assert.That(reviewer.HasStatusRow, Is.False,
                "the checks and sign-offs are the author's standing, not the reader's");
            Assert.That(reviewer.HasProposal, Is.True, "the proposal is still on their branch");
        });
    }

    [Test]
    public void ShowProposalBesideTask_onlyNamesAProposalThatIsNotTheTaskItself()
    {
        var sameNumber = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal("44")));
        var otherNumber = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal("51")));

        Assert.Multiple(() =>
        {
            Assert.That(sameNumber.ShowProposalBesideTask, Is.False, "it would repeat the task word for word");
            Assert.That(otherNumber.ShowProposalBesideTask, Is.True);
            Assert.That(otherNumber.ProposalLabel, Is.EqualTo("#51 Restock the storefront"));
        });
    }

    [Test]
    public void Subtitle_neverReportsTheWorkspaceLifecycleAsWhatTheAgentIsDoing()
    {
        // A running workspace whose agent has nothing in hand: the row says idle, and must not
        // also say Running, which is Agent-Up's state for the workspace rather than the agent's.
        var row = Row(Standing(TeamStandingKind.Idle, TeamRoleKind.None));

        Assert.Multiple(() =>
        {
            Assert.That(row.Subtitle, Is.EqualTo("codex"));
            Assert.That(row.Subtitle, Does.Not.Contain("Running"));
            Assert.That(row.StateLabel, Is.EqualTo("○ idle"));
        });
    }

    [Test]
    public void Subtitle_dropsEveryPartTheServerDidNotReport()
    {
        var full = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer,
            identity: new TeamIdentity("bob-aup[bot]", "Bob", true, "https://example.test/bob")));
        var bare = Row(Standing(TeamStandingKind.Working, TeamRoleKind.None, agent: null));

        Assert.Multiple(() =>
        {
            Assert.That(full.Subtitle, Is.EqualTo("Developer · @bob-aup[bot] · codex"));
            Assert.That(full.IdentityHandle, Is.EqualTo("@bob-aup[bot]"));
            Assert.That(full.IdentityToolTip, Is.EqualTo("Bob · https://example.test/bob"));
            Assert.That(bare.Subtitle, Is.Empty, "nothing is padded out with placeholders");
            Assert.That(bare.HasIdentity, Is.False);
            Assert.That(bare.HasRole, Is.False);
        });
    }

    [Test]
    public void HasDiagnosticCode_namesTheStopOnlyWhereItIsTheFailuresIdentity()
    {
        var halted = Row(Standing(TeamStandingKind.Halted, TeamRoleKind.Developer, code: "forge.unreachable"));
        var working = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, code: "forge.unreachable"));

        Assert.Multiple(() =>
        {
            Assert.That(halted.HasDiagnosticCode, Is.True);
            Assert.That(working.HasDiagnosticCode, Is.False, "on a running agent it only repeats the headline");
        });
    }

    [Test]
    public void ThreadSummary_countsOneThreadInTheSingular()
    {
        var one = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal(threads: 1)));
        var several = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal(threads: 3)));
        var none = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer, forge: Proposal()));

        Assert.Multiple(() =>
        {
            Assert.That(one.ThreadSummary, Is.EqualTo("1 unresolved review thread"));
            Assert.That(several.ThreadSummary, Is.EqualTo("3 unresolved review threads"));
            Assert.That(one.HasUnresolvedThreads, Is.True);
            Assert.That(none.HasUnresolvedThreads, Is.False);
        });
    }

    [TestCase(TeamStandingKind.WaitingOnYou, "◆ gate open")]
    [TestCase(TeamStandingKind.Halted, "▲ halted")]
    [TestCase(TeamStandingKind.Working, "● working")]
    [TestCase(TeamStandingKind.Idle, "○ idle")]
    public void StateLabel_namesWhatTheAgentNeeds(TeamStandingKind kind, string expected)
    {
        Assert.That(Row(Standing(kind, TeamRoleKind.Developer)).StateLabel, Is.EqualTo(expected));
    }

    [Test]
    public void StateColor_tellsTheFourStandingsApart()
    {
        var colors = new[]
        {
            TeamStandingKind.WaitingOnYou, TeamStandingKind.Halted,
            TeamStandingKind.Working, TeamStandingKind.Idle
        }.Select(kind => Row(Standing(kind, TeamRoleKind.Developer)).StateColor).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(colors[0], Is.EqualTo(AgentUpThemeColors.StatusWarning));
            Assert.That(colors[1], Is.EqualTo(AgentUpThemeColors.StatusDanger));
            Assert.That(colors[2], Is.EqualTo(AgentUpThemeColors.AccentBright));
            Assert.That(colors[3], Is.EqualTo(AgentUpThemeColors.TextMuted));
            Assert.That(colors, Is.Unique, "telling them apart at a glance is the whole job");
        });
    }

    [Test]
    public void TheRowCarriesTheBranchTheProposalAndWhyAnAgentStopped()
    {
        var halted = Row(Standing(TeamStandingKind.Halted, TeamRoleKind.Developer,
            reason: "The repository host stopped answering.", forge: Proposal()));
        var running = Row(Standing(TeamStandingKind.Working, TeamRoleKind.Developer));

        Assert.Multiple(() =>
        {
            Assert.That(halted.Branch, Is.EqualTo("feat/44-restock"), "the branch the work sits on");
            Assert.That(halted.HasReason, Is.True);
            Assert.That(halted.Reason, Is.EqualTo("The repository host stopped answering."));
            Assert.That(halted.ProposalUrl, Is.EqualTo("https://example.test/pull/44"));
            Assert.That(running.HasReason, Is.False, "nothing stopped it, so there is nothing to say");
            Assert.That(running.ProposalUrl, Is.Null, "no proposal, nowhere to open");
        });
    }

    private static StandingRowViewModel Row(TeamStanding standing) => new(standing);

    private static ForgeStanding Proposal(string number = "44", int threads = 0)
        => new("ready", number, "Restock the storefront", "https://example.test/pull/44",
            1, 0, 0, threads, "clean", null);

    private static TeamStanding Standing(
        TeamStandingKind kind,
        TeamRoleKind role,
        string name = "Bob",
        int? task = 44,
        string? agent = "codex",
        string? code = null,
        TeamIdentity? identity = null,
        ForgeStanding? forge = null,
        string? reason = null)
        => new(name.ToLowerInvariant(), name, "feat/44-restock", "Running", agent, kind, task,
            "Restock the storefront", reason, code, [], forge ?? ForgeStanding.None, role,
            Identity: identity);
}
