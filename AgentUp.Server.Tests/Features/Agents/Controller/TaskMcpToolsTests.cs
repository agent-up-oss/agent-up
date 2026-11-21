using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

/// <summary>
/// The three task tools an agent has. Everything around them - commit, push, proposal, review,
/// attachments - is the Server's, and these tests hold that line from the tool boundary.
/// </summary>
[TestFixture]
public sealed class TaskMcpToolsTests
{
    [Test]
    public async Task SubmitWork_turnsTheAuthorsChangesIntoAProposalForReview()
    {
        var team = await HeldAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));

        var result = await Tools(team).SubmitWork(team.Bob, "Replaced Hello with Ahoy.");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(team.Git.CommittedMessage, Is.EqualTo("Say Ahoy (#1)\n\nReplaced Hello with Ahoy."));
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(team.Progress.Satisfied(team.Bob, RoleTerminationKind.ProposalOpened), Is.True);
        });
    }

    [Test]
    public async Task SubmitWork_isRefusedToAReviewer()
    {
        var team = await HeldAsync();

        var result = await Tools(team).SubmitWork(team.Lucy, "I fixed it.");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Only an author submits work"));
        });
    }

    // A finding is a comment at a line, a screenshot is a file on the host the review links.
    [Test]
    public async Task SubmitReview_postsTheVerdictWithEachFindingAtItsLineAndEachScreenshotAttached()
    {
        var team = await InReviewAsync();
        team.Evidence.Captures["shot-1"] = ("image/png", [1, 2, 3]);
        team.Forge.Attachment = new AgentUp.Sdk.Forge.ForgeAttachment("screenshot-1.png", "https://example.test/attachments/1");

        var result = await Tools(team).SubmitReview(
            team.Lucy,
            "request_changes",
            "The comma is gone.",
            [new TaskFindingDto("src/greeting.js", 1, "Keep the comma after Ahoy.")],
            ["shot-1"]);
        var task = team.Queue.Get("acme", "widgets", "1")!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That((team.Forge.LastReview!.Event, team.Forge.LastReview.CommitId), Is.EqualTo(("REQUEST_CHANGES", team.Git.Commit)), "filed against the revision reviewed");
            Assert.That(team.Forge.LastReview.Comments, Is.EqualTo(new[] { new AgentUp.Sdk.Forge.ForgeReviewComment("src/greeting.js", 1, "Keep the comma after Ahoy.") }));
            Assert.That(team.Forge.LastReview.Body, Does.Contain("![Screenshot 1](https://example.test/attachments/1)"));
            Assert.That(task.Stage, Is.EqualTo(TaskStage.ChangesRequested));
            Assert.That(team.Applications.Stopped, Is.EqualTo(new[] { team.Lucy }), "submitting downs the reviewer's workspace");
        });
    }

    // The loop's own end-of-turn fallback must not post the same review a second time.
    [Test]
    public async Task SubmitReview_isTheOnlyReviewPostedForTheTurn()
    {
        var team = await InReviewAsync();

        await Tools(team).SubmitReview(team.Lucy, "approve", "Correct.");
        var finished = await team.Pipeline.FinishAsync(team.Lucy, Reviewer, Issue, new LoopRunResultBuilder().StopReason("submitted").Replying("Done. VERDICT: REQUEST_CHANGES").Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(finished.Published, Is.True);
            Assert.That(team.Forge.ReviewCalls, Is.EqualTo(1));
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Stage, Is.EqualTo(TaskStage.Approved));
        });
    }

    [TestCase("maybe", "verdict must be approve or request_changes")]
    [TestCase("approve", "No screenshot was found for missing-shot")]
    public async Task SubmitReview_refusesWhatItCannotPost(string verdict, string refusal)
    {
        var team = await InReviewAsync();

        var result = await Tools(team).SubmitReview(team.Lucy, verdict, "Looks fine.", null, ["missing-shot"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Message, Does.Contain(refusal));
            Assert.That(team.Forge.ReviewCalls, Is.Zero);
        });
    }

    [Test]
    public async Task SubmitReview_isRefusedToAnAuthor()
    {
        var team = await InReviewAsync();

        var result = await Tools(team).SubmitReview(team.Bob, "approve", "My own work is great.");

        Assert.That(result.Message, Does.Contain("Only a reviewer submits a review"));
    }

    [Test]
    public async Task GetTask_givesTheReviewerItsRevisionAndWhatToDoNext()
    {
        var team = await InReviewAsync();

        var result = await Tools(team).GetTask(team.Lucy);
        var task = (TaskBriefDto)result.Data!;

        Assert.Multiple(() =>
        {
            Assert.That(task.Role, Is.EqualTo("devReviewer"));
            Assert.That(task.Proposal!.Branch, Is.EqualTo("agent-up/issue-1"));
            Assert.That(task.Next, Does.Contain("submit_review"));
        });
    }

    // The author is called back with every finding at its line and every check with its log.
    [Test]
    public async Task GetTask_givesAnAuthorSentBackTheFindingsAndChecks()
    {
        var team = await InReviewAsync();
        team.Forge.Snapshot = new AgentUp.Sdk.Forge.ForgeHostSnapshot(
            null,
            [new AgentUp.Sdk.Forge.ForgeCheck("test", "completed", "failure", "https://example.test/runs/3")],
            [new AgentUp.Sdk.Forge.ForgeReviewThread("1", "", "Keep the comma.", false, "src/greeting.js", 1, "lucy")],
            null);
        await Tools(team).SubmitReview(team.Lucy, "request_changes", "One finding.");

        var task = (TaskBriefDto)(await Tools(team).GetTask(team.Bob)).Data!;

        Assert.Multiple(() =>
        {
            Assert.That(task.Stage, Is.EqualTo("changesRequested"));
            Assert.That(task.Findings, Is.EqualTo(new[] { new TaskFindingDto("src/greeting.js", 1, "Keep the comma.", "lucy") }));
            Assert.That(task.Checks.Single(), Is.EqualTo(new TaskCheckDto("test", "completed", "failure", "https://example.test/runs/3")));
            Assert.That(task.Reviews.Single().Verdict, Is.EqualTo("requestChanges"));
        });
    }

    [Test]
    public async Task GetTask_saysSoWhenTheMemberHoldsNothing()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);

        var result = await Tools(team).GetTask(team.Lucy);

        Assert.That(result.Succeeded, Is.False);
    }

    private static readonly AgentRoleDefinition Author = TeamRoles.For(TeamMemberKind.Author, null);
    private static readonly AgentRoleDefinition Reviewer = TeamRoles.For(TeamMemberKind.DevReviewer, null);
    private static readonly AssignedIssue Issue = ServerDomain.Issue().Titled("Say Ahoy").WithBody("In src/greeting.js replace Hello with Ahoy.").Build();

    private static TaskMcpTools Tools(TaskTeam team) => new(team.Work);

    private static async Task<TaskTeam> HeldAsync()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);
        team.Queue.Enqueue(new QueuedTask("1", "acme", "widgets", 1, "Say Ahoy", "In src/greeting.js replace Hello with Ahoy.", "https://example.test/1", DateTimeOffset.UnixEpoch));
        team.Queue.TryClaim(team.Bob, "acme", "widgets", "1");
        return team;
    }

    private static async Task<TaskTeam> InReviewAsync()
    {
        var team = await HeldAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));
        await team.Pipeline.FinishAsync(team.Bob, Author, Issue, new LoopRunResultBuilder().StopReason("settled").Replying("Done.").Build(), CancellationToken.None);
        team.Queue.TryClaimReview(team.Lucy, TeamMemberKind.DevReviewer, "acme", "widgets", "1");
        return team;
    }
}
