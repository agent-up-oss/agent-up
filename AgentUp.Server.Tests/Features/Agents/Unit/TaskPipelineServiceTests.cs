using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

/// <summary>
/// The Git and host work the Server does around a team member's turn, with Git and the forge
/// faked: what an author's finished turn becomes, and what a reviewer's verdict becomes.
/// </summary>
[TestFixture]
public sealed class TaskPipelineServiceTests
{
    [Test]
    public async Task PrepareAsync_startsAnAuthorFromItsBaseBranchAndLeadsWithTheIssue()
    {
        var team = await TeamAsync();

        var plan = await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        var task = team.Queue.Get("acme", "widgets", "1")!;

        Assert.Multiple(() =>
        {
            Assert.That(plan.Prompt, Does.StartWith("Issue #1: Say Ahoy"));
            Assert.That(task.Stage, Is.EqualTo(TaskStage.Implementing));
            Assert.That(task.BaseBranch, Is.EqualTo("main"));
        });
    }

    // The author takes the ticket; the Server does the host work for it, with its credential.
    [Test]
    public async Task PrepareAsync_startsAndPublishesTheTaskBranchAndAssignsTheIssueToTheAuthor()
    {
        var team = await TeamAsync();

        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(team.Git.StartedBranch, Is.EqualTo("agent-up/issue-1"));
            Assert.That(team.Transport.Pushed, Is.EqualTo(new[] { ("agent-up/issue-1", "bob-token") }));
            Assert.That(team.Forge.TeamWrites, Is.EqualTo(new[] { "bob-token: assign #1 -> bob" }));
        });
    }

    [Test]
    public async Task FinishAsync_turnsAnAuthorsChangesIntoAProposalOnItsTaskBranch()
    {
        var team = await TeamAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));

        var outcome = await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("Replaced Hello."), CancellationToken.None);
        var task = team.Queue.Get("acme", "widgets", "1")!;

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Published, Is.True, outcome.Cause);
            Assert.That(team.Git.Branch, Is.EqualTo("agent-up/issue-1"));
            Assert.That(team.Git.CommittedMessage, Is.EqualTo("Say Ahoy (#1)\n\nReplaced Hello."));
            Assert.That(team.Transport.Pushed[^1], Is.EqualTo(("agent-up/issue-1", "bob-token")), "pushed with the author's own credential");
            Assert.That(task.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(task.Proposal, Is.EqualTo(new TaskProposal("42", "https://example.test/42", "agent-up/issue-1", "main", 1, team.Git.Commit)));
            Assert.That(team.Progress.Satisfied(team.Bob, RoleTerminationKind.ProposalOpened), Is.True);
            Assert.That(team.Forge.TeamWrites, Does.Contain("bob-token: assign #42 -> bob"), "the proposal is assigned to its author too");
        });
    }

    [Test]
    public async Task FinishAsync_blocksAnAuthorThatChangedNothing()
    {
        var team = await TeamAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);

        var outcome = await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("I have committed the change."), CancellationToken.None);

        // The agent's word is not evidence: the working tree is.
        Assert.Multiple(() =>
        {
            Assert.That(outcome.StopReason, Is.EqualTo("nothing_to_propose"));
            Assert.That(team.Transport.Pushed, Has.Count.EqualTo(1), "only the empty task branch was pushed");
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Stage, Is.EqualTo(TaskStage.Implementing));
        });
    }

    [Test]
    public async Task FinishAsync_keepsTheTaskWhenThePushIsRefused()
    {
        var team = await TeamAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));
        team.Transport.PushFailure = "git push failed: 401";

        var outcome = await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("Done."), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.StopReason, Is.EqualTo("push_failed"));
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Proposal, Is.Null);
        });
    }

    [Test]
    public async Task FinishAsync_publishesACommitAnEarlierAttemptCouldNotPush()
    {
        var team = await TeamAsync();
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Refs["refs/remotes/origin/main"] = "base-commit";

        var outcome = await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("Done."), CancellationToken.None);

        Assert.That(outcome.Published, Is.True, outcome.Cause);
    }

    [Test]
    public async Task PrepareAsync_checksOutTheExactRevisionAndShowsAReviewerTheDiff()
    {
        var team = await InReviewAsync();
        team.Git.RangeDiff = "-Hello\n+Ahoy";

        var plan = await team.Pipeline.PrepareAsync(team.Lucy, Reviewer, Issue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(team.Transport.Fetched.Single(), Is.EqualTo(new[] { "agent-up/issue-1", "main" }));
            Assert.That(team.Git.ResetBranch, Is.EqualTo("agent-up/issue-1"));
            Assert.That(plan.Prompt, Does.Contain("-Hello\n+Ahoy"));
        });
    }

    // The author opened the proposal, so it is the author's credential that names the reviewer.
    [Test]
    public async Task PrepareAsync_namesTheReviewerOnTheProposalWithTheAuthorsCredential()
    {
        var team = await InReviewAsync();
        team.Forge.TeamWrites.Clear();

        await team.Pipeline.PrepareAsync(team.Lucy, Reviewer, Issue, CancellationToken.None);

        Assert.That(team.Forge.TeamWrites, Is.EqualTo(new[] { "bob-token: review !42 -> lucy" }));
    }

    [Test]
    public async Task PrepareAsync_namesTheReviewerWithItsOwnCredentialWhenTheProposalHasNoAuthor()
    {
        var team = await TaskTeamComposition.ComposeAsync(null);
        team.Queue.AdoptProposal(new QueuedTask(
            "42",
            "acme",
            "widgets",
            42,
            "Product cards",
            "",
            "https://example.test/42",
            DateTimeOffset.UnixEpoch,
            Stage: TaskStage.InReview,
            Proposal: new TaskProposal("42", "https://example.test/42", "feature", "main", 1, "aaa"),
            BaseBranch: "main"));
        team.Forge.TeamWrites.Clear();
        var adopted = ServerDomain.Issue().Numbered(42).Titled("Product cards").Build();

        await team.Pipeline.PrepareAsync(team.Lucy, Reviewer, adopted, CancellationToken.None);

        Assert.That(team.Forge.TeamWrites, Is.EqualTo(new[] { "lucy-token: review !42 -> lucy" }));
    }

    // Everything the host said comes back to the author: findings at their lines, failing checks, comments.
    [Test]
    public async Task PrepareAsync_givesAnAuthorSentBackEveryFindingAndFailingCheck()
    {
        var team = await InReviewAsync();
        team.Forge.Snapshot = new AgentUp.Sdk.Forge.ForgeHostSnapshot(
            null,
            [new AgentUp.Sdk.Forge.ForgeCheck("test", "completed", "failure", "https://example.test/runs/3")],
            [
                new AgentUp.Sdk.Forge.ForgeReviewThread("1", "", "Keep the comma.", false, "src/greeting.js", 1, "lucy"),
                new AgentUp.Sdk.Forge.ForgeReviewThread("2", "", "Ready for review.", false, null, null, "bob"),
                new AgentUp.Sdk.Forge.ForgeReviewThread("3", "", "Please also update the README.", false, null, null, "dana")
            ],
            null);
        team.Queue.Review("acme", "widgets", "1", new TaskReview(team.Lucy, TaskReviewVerdict.RequestChanges, 1, "One finding.", TeamMemberKind.DevReviewer), [TeamMemberKind.DevReviewer]);

        var plan = await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Prompt, Does.Contain("Review (code):\nOne finding."));
            Assert.That(plan.Prompt, Does.Contain("- src/greeting.js:1 (lucy): Keep the comma."));
            Assert.That(plan.Prompt, Does.Contain("- test: failure (https://example.test/runs/3)"));
            Assert.That(plan.Prompt, Does.Contain("- (dana) Please also update the README."));
            Assert.That(plan.Prompt, Does.Not.Contain("Ready for review."), "the author's own comment is not feedback");
            Assert.That(team.Forge.ReadBranches, Does.Contain("agent-up/issue-1"), "feedback is read for the task branch, whatever is checked out");
        });
    }

    [Test]
    public async Task PrepareAsync_tellsAProductReviewerWhatWasAskedButNeverShowsItTheCode()
    {
        var team = await InReviewAsync();
        team.Members.Set(team.Lucy, TeamMemberKind.PmReviewer);
        team.Git.RangeDiff = "-Hello\n+Ahoy";

        var plan = await team.Pipeline.PrepareAsync(team.Lucy, Reviewer, Issue, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(team.Git.ResetBranch, Is.EqualTo("agent-up/issue-1"), "the revision is what the workspace runs");
            Assert.That(team.Applications.Started, Is.EqualTo(new[] { team.Lucy }), "the Server starts it; the reviewer may not");
            Assert.That(plan.Prompt, Does.Contain("It is open in your browser at http://127.0.0.1:5701/"));
            Assert.That(plan.Prompt, Does.Contain("In src/greeting.js replace Hello with Ahoy."));
            Assert.That(plan.Prompt, Does.Not.Contain("+Ahoy"));
        });
    }

    [Test]
    public async Task PrepareAsync_refusesAProductReviewWhenTheRevisionDoesNotStart()
    {
        var team = await InReviewAsync(startError: "Storefront exited with code 1");
        team.Members.Set(team.Lucy, TeamMemberKind.PmReviewer);

        var plan = await team.Pipeline.PrepareAsync(team.Lucy, Reviewer, Issue, CancellationToken.None);

        Assert.That(plan.Refusal, Is.EqualTo("The revision under review did not start: Storefront exited with code 1"));
    }

    // Reviews are triggered by the push itself, so a person pushing to the task branch counts too.
    [Test]
    public async Task ListAwaitingAsync_putsAProposalBackInFrontOfAReviewerWhenAnyonePushesToIt()
    {
        var team = await InReviewAsync();
        team.Queue.Review("acme", "widgets", "1", new TaskReview(team.Lucy, TaskReviewVerdict.Approve, 1, "ok", TeamMemberKind.DevReviewer), [TeamMemberKind.DevReviewer]);
        var proposals = new CuratedProposalQueue(team.Queue, team.Host, team.Settings, team.GitRead);
        var approved = await proposals.ListAwaitingAsync(team.Lucy, CancellationToken.None);
        team.Git.Refs["refs/remotes/origin/agent-up/issue-1"] = "pushed-by-a-person";

        var afterPush = await proposals.ListAwaitingAsync(team.Lucy, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(approved, Is.Empty, "the head the reviewer approved has not moved");
            Assert.That(afterPush.Single().Number, Is.EqualTo(1));
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Proposal!.Revision, Is.EqualTo(2));
            Assert.That(team.Transport.Fetched[^1], Is.EqualTo(new[] { "agent-up/issue-1" }), "fetched with the reviewer's own credential");
        });
    }

    [Test]
    public async Task FinishAsync_submitsAReviewersVerdictAndApprovesWhenEveryReviewerApproved()
    {
        var team = await InReviewAsync();

        var outcome = await team.Pipeline.FinishAsync(team.Lucy, Reviewer, Issue, Turn("Correct and tested.\nVERDICT: APPROVE"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Published, Is.True, outcome.Cause);
            Assert.That(team.Forge.ReviewCalls, Is.EqualTo(1));
            Assert.That(team.Queue.Get("acme", "widgets", "1")!.Stage, Is.EqualTo(TaskStage.Approved));
            Assert.That(team.Progress.Satisfied(team.Lucy, RoleTerminationKind.ReviewSubmitted), Is.True);
            Assert.That(team.Applications.Stopped, Is.EqualTo(new[] { team.Lucy }), "a finished reviewer holds no running revision");
        });
    }

    // One dev reviewer is not enough when the team also has a product reviewer on the repository.
    [Test]
    public async Task FinishAsync_waitsForEachKindOfReviewTheTeamHas()
    {
        var team = await InReviewAsync();
        var pat = (await team.Workspaces.RegisterAsync(ServerDomain.Workspace().Named("pat").WithWorktreePath("/tmp/agent-up-pat").Build())).Id;
        team.Members.Set(pat, TeamMemberKind.PmReviewer);

        await team.Pipeline.FinishAsync(team.Lucy, Reviewer, Issue, Turn("Correct.\nVERDICT: APPROVE"), CancellationToken.None);
        var task = team.Queue.Get("acme", "widgets", "1")!;

        Assert.Multiple(() =>
        {
            Assert.That(task.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(task.DevReviewerWorkspaceId, Is.EqualTo(team.Lucy));
            Assert.That(team.Queue.AwaitingReview(pat, TeamMemberKind.PmReviewer, "acme", "widgets").Single().Id, Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task FinishAsync_postsNothingForAReviewWithoutAVerdict()
    {
        var team = await InReviewAsync();

        var outcome = await team.Pipeline.FinishAsync(team.Lucy, Reviewer, Issue, Turn("I could not open the URL."), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.StopReason, Is.EqualTo("no_verdict"));
            Assert.That(team.Forge.ReviewCalls, Is.Zero);
            Assert.That(team.Queue.AwaitingReview(team.Lucy, TeamMemberKind.DevReviewer, "acme", "widgets"), Has.Count.EqualTo(1), "the review is still owed");
        });
    }

    private static readonly AgentRoleDefinition Author = TeamRoles.For(TeamMemberKind.Author, null);
    private static readonly AgentRoleDefinition Reviewer = TeamRoles.For(TeamMemberKind.DevReviewer, null);
    private static readonly AssignedIssue Issue = ServerDomain.Issue().Titled("Say Ahoy").WithBody("In src/greeting.js replace Hello with Ahoy.").Build();

    private static LoopRunResult Turn(string reply) => new LoopRunResultBuilder().StopReason("settled").Replying(reply).Build();

    private static async Task<TaskTeam> InReviewAsync(string? startError = null)
    {
        var team = await TeamAsync(startError);
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));
        await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("Done."), CancellationToken.None);
        team.Transport.Fetched.Clear();
        return team;
    }

    private static async Task<TaskTeam> TeamAsync(string? startError = null)
    {
        var team = await TaskTeamComposition.ComposeAsync(startError);
        team.Queue.Enqueue(new QueuedTask("1", "acme", "widgets", 1, "Say Ahoy", "In src/greeting.js replace Hello with Ahoy.", "https://example.test/1", DateTimeOffset.UnixEpoch));
        team.Queue.TryClaim(team.Bob, "acme", "widgets", "1");
        return team;
    }
}
