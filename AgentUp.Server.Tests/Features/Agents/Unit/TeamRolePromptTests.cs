using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class TeamRolePromptTests
{
    [TestCase("Looks right.\n\nVERDICT: APPROVE", TaskReviewVerdict.Approve, "Looks right.")]
    [TestCase("The test is wrong.\n**VERDICT: REQUEST_CHANGES**", TaskReviewVerdict.RequestChanges, "The test is wrong.")]
    [TestCase("verdict: request changes", TaskReviewVerdict.RequestChanges, "")]
    [TestCase("I could not find the URL.", TaskReviewVerdict.Comment, "I could not find the URL.")]
    public void ReviewVerdictText_readsTheLastVerdictLine(string reply, TaskReviewVerdict verdict, string body)
    {
        var (read, rest) = ReviewVerdictText.Read(reply);

        Assert.That((read, rest), Is.EqualTo((verdict, body)));
    }

    [TestCase(TeamMemberKind.Author, RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened)]
    [TestCase(TeamMemberKind.DevReviewer, RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted)]
    [TestCase(TeamMemberKind.PmReviewer, RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted)]
    public void TeamRoles_giveEachMemberKindItsOwnRole(TeamMemberKind member, RoleTriggerKind trigger, RoleTerminationKind termination)
    {
        var role = TeamRoles.For(member, null);

        Assert.That((role.Trigger, role.Termination), Is.EqualTo((trigger, termination)));
    }

    [Test]
    public void TeamRoles_keepADeclaredRole()
    {
        var declared = new AgentRoleDefinition("Custom", [], RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened);

        Assert.That(TeamRoles.For(TeamMemberKind.DevReviewer, declared), Is.SameAs(declared));
    }

    [Test]
    public void Author_leadsWithTheIssueAndCarriesRequestedChanges()
    {
        var task = ReviewedTask() with { Stage = TaskStage.ChangesRequested };

        var prompt = TaskPrompts.Author(TeamRoles.For(TeamMemberKind.Author, null), task);

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.StartWith("Issue #3: Call the bell bronze"));
            Assert.That(prompt, Does.Contain("Rename the test too."));
            Assert.That(prompt, Does.Not.Contain("Fine by me."), "only the requests for changes are the author's to address");
            Assert.That(prompt, Does.Not.Contain("https://example.test/issues/3"), "a URL invites an agent to fetch instead of edit");
        });
    }

    [Test]
    public void Reviewer_carriesTheDiffInlineAndCutsALongOne()
    {
        var role = TeamRoles.For(TeamMemberKind.DevReviewer, null);

        var small = TaskPrompts.Reviewer(role, ReviewedTask(), "-Brass\n+Bronze");
        var large = TaskPrompts.Reviewer(role, ReviewedTask(), new string('x', 20_000));

        Assert.Multiple(() =>
        {
            Assert.That(small, Does.Contain("```diff\n-Brass\n+Bronze\n```"));
            Assert.That(small, Does.EndWith("VERDICT: REQUEST_CHANGES."));
            Assert.That(large, Does.Contain("diff cut at 12000 characters"));
        });
    }

    [Test]
    public void Commit_namesTheIssueOnTheFirstRevisionAndTheReviewAfter()
    {
        var first = ReviewedTask() with { Proposal = null };

        Assert.Multiple(() =>
        {
            Assert.That(TaskPrompts.Commit(first, "Renamed it."), Is.EqualTo("Call the bell bronze (#3)\n\nRenamed it."));
            Assert.That(TaskPrompts.Commit(ReviewedTask(), null), Is.EqualTo("Address review of #3 (revision 2)"));
            Assert.That(TaskPrompts.ProposalBody(first, null), Is.EqualTo("Closes #3."));
        });
    }

    private static QueuedTask ReviewedTask() => new(
        "3", "harbor", "storefront", 3, "Call the bell bronze", "In src/catalog.js replace Brass with Bronze.",
        "https://example.test/issues/3", DateTimeOffset.UnixEpoch,
        HeldByWorkspaceId: "bob",
        Stage: TaskStage.InReview,
        Proposal: new TaskProposal("4", "https://example.test/pulls/4", "agent-up/issue-3", "main", 1),
        Reviews:
        [
            new TaskReview("lucy", TaskReviewVerdict.RequestChanges, 1, "Rename the test too."),
            new TaskReview("hector", TaskReviewVerdict.Approve, 1, "Fine by me.")
        ]);
}
