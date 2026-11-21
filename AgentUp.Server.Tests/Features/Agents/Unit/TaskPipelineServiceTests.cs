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
            Assert.That(team.Transport.Pushed, Is.EqualTo(new[] { ("agent-up/issue-1", "bob-token") }), "pushed with the author's own credential");
            Assert.That(task.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(task.Proposal, Is.EqualTo(new TaskProposal("42", "https://example.test/42", "agent-up/issue-1", "main", 1, team.Git.Commit)));
            Assert.That(team.Progress.Satisfied(team.Bob, RoleTerminationKind.ProposalOpened), Is.True);
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
            Assert.That(team.Transport.Pushed, Is.Empty);
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
        team.Git.Branch = "agent-up/issue-1";

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
        team.Queue.Review("acme", "widgets", "1", new TaskReview(team.Lucy, TaskReviewVerdict.Approve, 1, "ok"), [team.Lucy]);
        var proposals = new CuratedProposalQueue(team.Queue, team.Host, team.GitRead);
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
            Assert.That(team.Queue.AwaitingReview(team.Lucy, "acme", "widgets"), Has.Count.EqualTo(1), "the review is still owed");
        });
    }

    private static readonly AgentRoleDefinition Author = TeamRoles.For(TeamMemberKind.Author, null);
    private static readonly AgentRoleDefinition Reviewer = TeamRoles.For(TeamMemberKind.DevReviewer, null);
    private static readonly AssignedIssue Issue = ServerDomain.Issue().Titled("Say Ahoy").WithBody("In src/greeting.js replace Hello with Ahoy.").Build();

    private static LoopRunResult Turn(string reply) => new LoopRunResultBuilder().StopReason("settled").Replying(reply).Build();

    private static async Task<Team> InReviewAsync(string? startError = null)
    {
        var team = await TeamAsync(startError);
        await team.Pipeline.PrepareAsync(team.Bob, Author, Issue, CancellationToken.None);
        team.Git.Changes.Add(new GitChangeEntry("src/greeting.js", GitChangeStatus.Modified));
        await team.Pipeline.FinishAsync(team.Bob, Author, Issue, Turn("Done."), CancellationToken.None);
        team.Transport.Fetched.Clear();
        return team;
    }

    private static async Task<Team> TeamAsync(string? startError = null)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var bob = (await workspaces.RegisterAsync(ServerDomain.Workspace().Build())).Id;
        var lucy = (await workspaces.RegisterAsync(ServerDomain.SecondWorkspace().Build())).Id;
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var members = new TeamMemberController(new TeamMemberService(settings, workspaces));
        members.Set(lucy, TeamMemberKind.DevReviewer);
        var forge = new StubForgeCapability();
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(bob, "github", "bob-token");
        credentials.Write(lucy, "github", "lucy-token");
        var transport = new RecordingForgeGitTransport();
        var forgeController = new ForgeController(new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(forge),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://example.test/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("unused"),
            null,
            members,
            transport));
        var git = new FakeGitWorkingTreeProvider();
        var changes = new GitChangeTreeService(workspaces, git, new FakeWorkspacePromptGuard());
        var queue = new TaskQueueService(new MemoryTaskQueueStore());
        queue.Enqueue(new QueuedTask("1", "acme", "widgets", 1, "Say Ahoy", "In src/greeting.js replace Hello with Ahoy.", "https://example.test/1", DateTimeOffset.UnixEpoch));
        queue.TryClaim(bob, "acme", "widgets", "1");
        var progress = new MemoryRoleProgress();
        var applications = new FixedReviewApplicationHost(["http://127.0.0.1:5701/"], startError);
        var pipeline = new TaskPipelineService(queue, forgeController, new GitWorkspaceController(changes), new GitQueryController(changes), workspaces, settings, progress, applications);
        return new Team(pipeline, queue, git, transport, forge, progress, bob, lucy, forgeController, new GitQueryController(changes), members, applications);
    }

    private sealed record Team(
        ITaskPipeline Pipeline,
        TaskQueueService Queue,
        FakeGitWorkingTreeProvider Git,
        RecordingForgeGitTransport Transport,
        StubForgeCapability Forge,
        MemoryRoleProgress Progress,
        string Bob,
        string Lucy,
        ForgeController Host,
        GitQueryController GitRead,
        TeamMemberController Members,
        FixedReviewApplicationHost Applications);

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
