using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class TeamMemberGrantsTests
{
    [Test]
    public void For_authorReadsAndWritesButDoesNotReview()
    {
        var grants = TeamMemberGrants.For(TeamMemberKind.Author);

        Assert.Multiple(() =>
        {
            Assert.That(grants.ReadsRepository && grants.WritesRepository, Is.True);
            Assert.That(grants.SubmitsReviews, Is.False);
            Assert.That(grants.Fixture, Is.EqualTo(AgentFixtureKind.CodingAgent));
            Assert.That(grants.OperationPermissions, Does.Contain("git.write"));
        });
    }

    [Test]
    public void For_devReviewerReadsButNeitherWritesNorDrivesTheBrowser()
    {
        var grants = TeamMemberGrants.For(TeamMemberKind.DevReviewer);

        Assert.Multiple(() =>
        {
            Assert.That(grants.ReadsRepository, Is.True);
            Assert.That(grants.WritesRepository || grants.ControlsBrowser, Is.False);
            Assert.That(grants.SubmitsReviews, Is.True);
            Assert.That(grants.Fixture, Is.EqualTo(AgentFixtureKind.CodingAgent));
            Assert.That(grants.OperationPermissions, Does.Not.Contain("git.write").And.Not.Contain("browser.control"));
        });
    }

    [Test]
    public void For_pmReviewerDrivesTheBrowserWithoutReadingTheRepository()
    {
        var grants = TeamMemberGrants.For(TeamMemberKind.PmReviewer);

        Assert.Multiple(() =>
        {
            Assert.That(grants.ReadsRepository || grants.WritesRepository, Is.False);
            Assert.That(grants.ControlsBrowser && grants.SubmitsReviews, Is.True);
            Assert.That(grants.Fixture, Is.EqualTo(AgentFixtureKind.InProcess));
            Assert.That(grants.OperationPermissions, Does.Contain("browser.control").And.Not.Contain("git.read"));
        });
    }

    [Test]
    public void Exceeds_namesRulesOutsideTheGrantAndPassesDenyRules()
    {
        var reviewer = TeamMemberGrants.DevReviewer;
        var pm = TeamMemberGrants.PmReviewer;

        Assert.Multiple(() =>
        {
            Assert.That(reviewer.Exceeds(Rule(PermissionRuleKind.WorktreeWrite, true)), Does.Contain("writing"));
            Assert.That(reviewer.Exceeds(Rule(PermissionRuleKind.Execute, true)), Does.Contain("named commands"));
            Assert.That(reviewer.Exceeds(Rule(PermissionRuleKind.Execute, true, "dotnet test")), Is.Null);
            Assert.That(reviewer.Exceeds(Rule(PermissionRuleKind.WorktreeWrite, false)), Is.Null);
            Assert.That(pm.Exceeds(Rule(PermissionRuleKind.WorktreeRead, true)), Does.Contain("reading"));
            Assert.That(pm.Exceeds(Rule(PermissionRuleKind.Execute, true, "dotnet test")), Does.Contain("executing"));
            Assert.That(pm.Exceeds(Rule(PermissionRuleKind.Network, true)), Is.Null);
        });
    }

    private static PermissionRule Rule(PermissionRuleKind kind, bool allow, string? scope = null) =>
        new("rule", kind, allow, scope);

    [Test]
    public void FixtureOf_onlyTheOllamaAgentRunsInProcess()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InProcessAgents.FixtureOf("ollama"), Is.EqualTo(AgentFixtureKind.InProcess));
            Assert.That(InProcessAgents.FixtureOf("claude"), Is.EqualTo(AgentFixtureKind.CodingAgent));
            Assert.That(TeamMemberGrants.PmReviewer.AllowsFixture(AgentFixtureKind.CodingAgent), Is.False);
        });
    }
}
