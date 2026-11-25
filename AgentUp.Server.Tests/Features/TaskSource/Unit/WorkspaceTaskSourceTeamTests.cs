using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class WorkspaceTaskSourceTeamTests
{
    [Test]
    public async Task ReviewersOnTheRepositoryAreTheLoginsThePullInferencesNeed()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);
        var source = new WorkspaceTaskSourceTeam(team.Host, team.Workspaces, team.Settings);

        var members = await source.ForAsync("acme", "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(members.Kinds, Is.EqualTo(new[] { "devReviewer" }));
            Assert.That(members.KindByLogin["lucy"], Is.EqualTo("devReviewer"));
            Assert.That(members.KindByLogin.ContainsKey("bob"), Is.False, "an author is not a reviewer");
        });
    }

    [Test]
    public async Task AReviewerWhoseHostAccountCannotBeReadStillOwesThatKindOfReview()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);
        team.Forge.ReadFailure = new HttpRequestException("offline");
        var source = new WorkspaceTaskSourceTeam(team.Host, team.Workspaces, team.Settings);

        var members = await source.ForAsync("acme", "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(members.Kinds, Is.EqualTo(new[] { "devReviewer" }));
            Assert.That(members.KindByLogin, Is.Empty);
        });
    }

    [Test]
    public async Task ADifferentRepositoryHasNoReviewersFromThisTeam()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);
        var source = new WorkspaceTaskSourceTeam(team.Host, team.Workspaces, team.Settings);

        var members = await source.ForAsync("other", "repo", CancellationToken.None);

        Assert.That(members.Kinds, Is.Empty);
    }
}
