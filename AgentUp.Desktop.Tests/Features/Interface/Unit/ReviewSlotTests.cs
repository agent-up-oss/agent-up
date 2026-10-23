using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// A proposal's two review slots, filled by matching each verdict the host reported against the
/// accounts the team acts as.
/// </summary>
[TestFixture]
public sealed class ReviewSlotTests
{
    [Test]
    public async Task ReadAsync_fillsEachSlotFromTheTeammateWhoSuppliesThatReview()
    {
        var standings = await ReadTeamAsync(
            TeamStandingComposition.Verdict("hector-aup[bot]", "APPROVED"),
            TeamStandingComposition.Verdict("lucy-aup[bot]", "COMMENTED"));

        var slots = standings.Single(item => item.WorkspaceId == "bob").Slots;

        Assert.Multiple(() =>
        {
            Assert.That(slots.Select(slot => slot.Kind),
                Is.EqualTo(new[] { ReviewSlotKind.Dev, ReviewSlotKind.Pm }));
            Assert.That(slots[0].Mark, Is.EqualTo(ReviewMark.Approved), "Hector supplies the dev review");
            Assert.That(slots[1].Mark, Is.EqualTo(ReviewMark.Warning), "Lucy supplies the pm review");
        });
    }

    [Test]
    public async Task ReadAsync_changesRequestedBlocksItsSlotAndAnUnreviewedSlotStaysEmpty()
    {
        var standings = await ReadTeamAsync(
            TeamStandingComposition.Verdict("hector-aup[bot]", "CHANGES_REQUESTED"));

        var slots = standings.Single(item => item.WorkspaceId == "bob").Slots;

        Assert.Multiple(() =>
        {
            Assert.That(slots[0].Mark, Is.EqualTo(ReviewMark.Blocked));
            Assert.That(slots[1].Mark, Is.EqualTo(ReviewMark.None));
            Assert.That(slots[0].Glyph, Is.EqualTo("✗"));
            Assert.That(slots[1].Glyph, Is.EqualTo("–"));
        });
    }

    [Test]
    public async Task ReadAsync_averdictFromOutsideTheTeamFillsNoSlot()
    {
        var standings = await ReadTeamAsync(TeamStandingComposition.Verdict("someone-else", "APPROVED"));

        var slots = standings.Single(item => item.WorkspaceId == "bob").Slots;

        Assert.That(slots.Select(slot => slot.Mark), Is.All.EqualTo(ReviewMark.None));
    }

    [Test]
    public async Task ReadAsync_aDismissedOrPendingVerdictHasNotBeenSaidYet()
    {
        var dismissed = await ReadTeamAsync(TeamStandingComposition.Verdict("hector-aup[bot]", "DISMISSED"));
        var pending = await ReadTeamAsync(TeamStandingComposition.Verdict("hector-aup[bot]", "PENDING"));

        Assert.Multiple(() =>
        {
            Assert.That(dismissed.Single(item => item.WorkspaceId == "bob").Slots[0].Mark, Is.EqualTo(ReviewMark.None));
            Assert.That(pending.Single(item => item.WorkspaceId == "bob").Slots[0].Mark, Is.EqualTo(ReviewMark.None));
        });
    }

    [Test]
    public async Task ReadAsync_anAgentWithNoProposalHasNoSlotsAtAll()
    {
        var standings = await ReadTeamAsync();

        Assert.That(standings.Single(item => item.WorkspaceId == "hector").Slots, Is.Empty,
            "no proposal is a different thing from a proposal nobody has reviewed");
    }

    /// <summary>
    /// Bob holds the proposal; Hector supplies the dev review and Lucy the pm review, each from
    /// their own account. Only Bob's forge carries verdicts.
    /// </summary>
    private static async Task<IReadOnlyList<TeamStanding>> ReadTeamAsync(params ForgeReviewDto[] verdicts)
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Sessions["bob"] = TeamStandingComposition.Session("bob", role: TeamStandingComposition.Developer());
        agents.Sessions["hector"] = TeamStandingComposition.Session("hector", role: TeamStandingComposition.Reviewer("Dev"));
        agents.Sessions["lucy"] = TeamStandingComposition.Session("lucy", role: TeamStandingComposition.Reviewer("Pm"));
        forge.Hosting["bob"] = TeamStandingComposition.Hosting(
            account: TeamStandingComposition.Account("bob-aup[bot]"), reviews: verdicts);
        forge.Hosting["hector"] = TeamStandingComposition.Hosting(
            availability: "noProposal", number: null, mergeState: null,
            account: TeamStandingComposition.Account("hector-aup[bot]"));
        forge.Hosting["lucy"] = TeamStandingComposition.Hosting(
            account: TeamStandingComposition.Account("lucy-aup[bot]"));

        return await TeamStandingComposition.Controller(agents, forge).ReadAsync(
            [.. new[] { "bob", "hector", "lucy" }.Select(id => new TeamMember(id, id, "main", "Running"))]);
    }
}

[TestFixture]
public sealed class ChecksStateTests
{
    [Test]
    public void Checks_failingBeatsRunningBeatsPassing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Forge(passed: 1, failed: 1, running: 1).Checks, Is.EqualTo(ChecksState.Failed));
            Assert.That(Forge(passed: 1, running: 1).Checks, Is.EqualTo(ChecksState.Running));
            Assert.That(Forge(passed: 2).Checks, Is.EqualTo(ChecksState.Passed));
            Assert.That(Forge(skipped: 2).Checks, Is.EqualTo(ChecksState.Skipped));
            Assert.That(ForgeStanding.None.Checks, Is.EqualTo(ChecksState.None));
        });
    }

    [Test]
    public void ChecksTotal_countsEveryCheckHoweverItEnded()
    {
        Assert.That(Forge(passed: 24, failed: 4, running: 2, skipped: 4).ChecksTotal, Is.EqualTo(34));
        Assert.That(ForgeStanding.None.ChecksTotal, Is.Zero);
    }

    private static ForgeStanding Forge(int passed = 0, int failed = 0, int running = 0, int skipped = 0)
        => new("ready", "44", "Title", "u", passed, failed, running, 0, "clean", null, skipped);
}
