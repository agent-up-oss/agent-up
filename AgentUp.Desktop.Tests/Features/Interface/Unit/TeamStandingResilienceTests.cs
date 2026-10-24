using System.Text.Json;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// What the team read does when one workspace misbehaves, and where an agent stands when no
/// unattended loop is running. Both reads go out together, so a failure that escapes one of them
/// costs every other agent its standing.
/// </summary>
[TestFixture]
public sealed class TeamStandingResilienceTests
{
    [Test]
    public async Task ReadAsync_aTimedOutSessionCostsOnlyThatAgentItsSession()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        agents.Failures["lucy"] = new TaskCanceledException("the read ran out of time");
        agents.Sessions["bob"] = TeamStandingComposition.Session("bob", state: "running");

        var standings = await Read(agents, forge, "bob", "lucy");

        Assert.Multiple(() =>
        {
            Assert.That(standings, Has.Count.EqualTo(2), "one slow agent does not hold up the rest");
            Assert.That(standings.Single(item => item.WorkspaceId == "bob").Kind,
                Is.EqualTo(TeamStandingKind.Working));
            Assert.That(standings.Single(item => item.WorkspaceId == "lucy").Kind,
                Is.EqualTo(TeamStandingKind.Idle), "nothing was read, so nothing is claimed");
        });
    }

    [Test]
    public async Task ReadAsync_aBodyTheServerDidNotWriteProperlyCostsOnlyThatAgent()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        forge.Failures["hector"] = new JsonException("unterminated object");
        forge.Hosting["bob"] = TeamStandingComposition.Hosting();

        var standings = await Read(agents, forge, "bob", "hector");

        Assert.Multiple(() =>
        {
            Assert.That(standings, Has.Count.EqualTo(2));
            Assert.That(standings.Single(item => item.WorkspaceId == "bob").Forge.HasProposal, Is.True);
            Assert.That(standings.Single(item => item.WorkspaceId == "hector").Forge.HasProposal, Is.False);
        });
    }

    [Test]
    public void ReadAsync_cancellationTheCallerAskedForStillStops()
    {
        var agents = new StandingAgentFake();
        var forge = new StandingForgeFake();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        agents.Failures["bob"] = new OperationCanceledException(cancelled.Token);

        // That one belongs to everybody, so it is not swallowed as one workspace's own failure.
        Assert.That(async () => await TeamStandingComposition.Controller(agents, forge).ReadAsync(
                [new TeamMember("bob", "Bob", "main", "Running")], cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase("running", TeamStandingKind.Working)]
    [TestCase("awaiting_input", TeamStandingKind.WaitingOnYou)]
    [TestCase("error", TeamStandingKind.Halted)]
    [TestCase("ready", TeamStandingKind.Idle)]
    public async Task ReadAsync_withoutALoopTheSessionStateSaysWhereTheAgentStands(
        string state, TeamStandingKind expected)
    {
        var agents = new StandingAgentFake();
        agents.Sessions["bob"] = TeamStandingComposition.Session("bob", state: state);

        var standing = (await Read(agents, new StandingForgeFake(), "bob")).Single();

        Assert.That(standing.Kind, Is.EqualTo(expected));
    }

    [Test]
    public async Task ReadAsync_aWorkspaceWithNoSessionAtAllHoldsNothing()
    {
        var standing = (await Read(new StandingAgentFake(), new StandingForgeFake(), "bob")).Single();

        Assert.That(standing.Kind, Is.EqualTo(TeamStandingKind.Idle));
    }

    private static Task<IReadOnlyList<TeamStanding>> Read(
        StandingAgentFake agents,
        StandingForgeFake forge,
        params string[] workspaceIds)
        => TeamStandingComposition.Controller(agents, forge).ReadAsync(
            workspaceIds.Select(id => new TeamMember(id, id, "main", "Running")).ToArray());
}
