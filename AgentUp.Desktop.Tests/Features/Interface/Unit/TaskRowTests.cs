using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// One row per task, naming everyone on it. The row's standing is the most urgent thing any of
/// its agents needs, because a queue that reports the calmest agent hides the one at a gate.
/// </summary>
[TestFixture]
public sealed class TaskRowTests
{
    [Test]
    public void TaskLabel_namesTheIssueAndTheTitleWhicheverAgentReportsIt()
    {
        var row = new TaskRowViewModel(44, [Standing("bob", TeamStandingKind.Working, title: null),
            Standing("lucy", TeamStandingKind.Working, title: "Restock the storefront")]);

        Assert.That(row.TaskLabel, Is.EqualTo("#44 · Restock the storefront"));
    }

    [Test]
    public void TaskLabel_fallsBackToTheNumberWhenNobodyReportsATitle()
    {
        var row = new TaskRowViewModel(44, [Standing("bob", TeamStandingKind.Working, title: null)]);

        Assert.That(row.TaskLabel, Is.EqualTo("#44"));
    }

    [TestCase(TeamStandingKind.WaitingOnYou, TeamStandingKind.Idle, TeamStandingKind.WaitingOnYou)]
    [TestCase(TeamStandingKind.Halted, TeamStandingKind.Working, TeamStandingKind.Halted)]
    [TestCase(TeamStandingKind.Working, TeamStandingKind.Idle, TeamStandingKind.Working)]
    [TestCase(TeamStandingKind.Idle, TeamStandingKind.Idle, TeamStandingKind.Idle)]
    public void Kind_takesTheMostUrgentStandingOnTheTask(
        TeamStandingKind first, TeamStandingKind second, TeamStandingKind expected)
    {
        var row = new TaskRowViewModel(44, [Standing("bob", first), Standing("lucy", second)]);

        Assert.That(row.Kind, Is.EqualTo(expected));
    }

    [Test]
    public void Kind_putsAGateAheadOfAHaltWhicheverAgentReportsItFirst()
    {
        var gateLast = new TaskRowViewModel(44,
            [Standing("bob", TeamStandingKind.Halted), Standing("lucy", TeamStandingKind.WaitingOnYou)]);

        Assert.That(gateLast.Kind, Is.EqualTo(TeamStandingKind.WaitingOnYou));
    }

    [Test]
    public void Rank_ordersAGateAboveAHaltAboveWorkAboveIdle()
    {
        var ranks = new[]
        {
            TeamStandingKind.WaitingOnYou, TeamStandingKind.Halted,
            TeamStandingKind.Working, TeamStandingKind.Idle
        }.Select(kind => new TaskRowViewModel(44, [Standing("bob", kind)]).Rank).ToArray();

        Assert.That(ranks, Is.Ordered, "the queue sorts on this, so it has to rise with calm");
    }

    [TestCase(TeamStandingKind.WaitingOnYou, "◆ gate open")]
    [TestCase(TeamStandingKind.Halted, "▲ halted")]
    [TestCase(TeamStandingKind.Working, "● working")]
    [TestCase(TeamStandingKind.Idle, "○ idle")]
    public void StateLabel_namesWhatTheTaskNeeds(TeamStandingKind kind, string expected)
    {
        var row = new TaskRowViewModel(44, [Standing("bob", kind)]);

        Assert.That(row.StateLabel, Is.EqualTo(expected));
    }

    [Test]
    public void StateColor_tellsTheFourStandingsApart()
    {
        var colors = new[]
        {
            TeamStandingKind.WaitingOnYou, TeamStandingKind.Halted,
            TeamStandingKind.Working, TeamStandingKind.Idle
        }.Select(kind => new TaskRowViewModel(44, [Standing("bob", kind)]).StateColor).ToArray();

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
    public void Agents_namesEveryoneOnTheTaskWithWhatTheyAreFor()
    {
        var row = new TaskRowViewModel(44,
        [
            Standing("bob", TeamStandingKind.Working, role: TeamRoleKind.Developer),
            Standing("lucy", TeamStandingKind.Idle, role: TeamRoleKind.None)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(row.Agents.Select(agent => agent.DisplayName), Is.EqualTo(new[] { "bob", "lucy" }));
            Assert.That(row.Agents[0].RoleLabel, Is.EqualTo("Developer"));
            Assert.That(row.Agents[0].HasRole, Is.True);
            Assert.That(row.Agents[1].RoleLabel, Is.Empty, "an unreported role is dropped, not padded out");
            Assert.That(row.Agents[1].HasRole, Is.False);
        });
    }

    private static TeamStanding Standing(
        string name, TeamStandingKind kind, string? title = null,
        TeamRoleKind role = TeamRoleKind.None)
        => new(name, name, "feat/44-restock", "Running", "codex", kind, 44, title,
            null, null, [], ForgeStanding.None, role);
}
