using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Shared.Interfaces;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitQueueMcpToolsTests
{
    private FakeCommitsQueueProvider _queue = null!;
    private FakeCommitsGitProvider _git = null!;
    private WorkspaceRegistry _registry = null!;
    private CommitQueueMcpTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _queue = new FakeCommitsQueueProvider();
        _git = new FakeCommitsGitProvider();
        _registry = ServerTestComposition.CreateRegistry();
        var controller = new CommitsController(new CommitsService(_queue, _git, new CommitPolicyProvider()));
        _tools = new CommitQueueMcpTools(new CommitQueueMcpService(
            controller,
            ServerTestComposition.CreateWorkspaceTargetController(_registry)));
    }

    [Test]
    public async Task EnqueueCommit_ReturnsSuccess_WhenCommitIsEnqueued()
    {
        var result = await _tools.EnqueueCommit(
            "feat/new-thing",
            "feat(new-thing): add new thing",
            ["src/Thing.cs"],
            worktreePath: "/repos/app",
            cancellationToken: CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Does.Contain("The tracked files have been restored to their pre-change state"));
        Assert.That(result.Message, Does.Contain("Do NOT re-apply or modify those files"));
        Assert.That(result.Data, Is.TypeOf<CommitsEnqueueResult>());
        Assert.That(_queue.Stored!.Commits, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GuardCommits_BlocksNewWork_WhenQueueHasEntry()
    {
        await EnqueueAsync();

        var result = await _tools.GuardCommits(worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("blocked starting new work"));
        var guard = (CommitGuardResult)result.Data!;
        Assert.That(guard.Blockers.Single(), Does.Contain("still queued"));
    }

    [Test]
    public async Task GuardCommits_DirectsDependentWorkToManagedQueueTip()
    {
        var queue = new FakeCommitsQueueProvider(ServerDomain.Queue()
            .AtVersion(3)
            .With(ServerDomain.CommitEntry()
                .For("Commits")
                .Saying("feat(commits): queued")
                .Touching(["a.cs"])
                .WithId("entry")
                .WithPatchId("patch")
                .WithParentCommit("base")
                .WithProposalCommit("tip")
                .InState("ready")
                .Build())
            .WithBaseCommit("base")
            .WithTipCommit("tip")
            .InWorktree("/managed/queue")
            .AtGeneration(1)
            .Build());
        var commits = new CommitsService(queue, new FakeCommitsGitProvider(), new CommitPolicyProvider());
        var tools = new CommitQueueMcpTools(new CommitQueueMcpService(
            new CommitsController(commits),
            ServerTestComposition.CreateWorkspaceTargetController(ServerTestComposition.CreateRegistry())));

        var result = await tools.GuardCommits(worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Does.Contain("Continue dependent work"));
        Assert.That(((CommitGuardResult)result.Data!).ContinueWorktreePath, Is.EqualTo("/managed/queue"));
    }

    [Test]
    public async Task GuardCommits_BlocksNewWork_WhenGitOperationIsActive()
    {
        _git.OperationState = new GitOperationState("merge", true);

        var result = await _tools.GuardCommits(worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.False);
        var guard = (CommitGuardResult)result.Data!;
        Assert.That(guard.Blockers.Single(), Does.Contain("Git merge"));
    }

    [Test]
    public async Task EnqueueReviewFixCommit_StoresReviewIssueId()
    {
        var result = await _tools.EnqueueReviewFixCommit(
            "review-42",
            "Commits",
            "fix(commits): block merge queue use",
            ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"],
            worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(_queue.Stored!.Commits.Single().ReviewIssueId, Is.EqualTo("review-42"));
    }

    [Test]
    public async Task EnqueueReviewFixCommit_RejectsMissingReviewIssueId()
    {
        var result = await _tools.EnqueueReviewFixCommit(
            "",
            "Commits",
            "fix(commits): block merge queue use",
            ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"],
            worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("reviewIssueId"));
    }

    [Test]
    public async Task GetCommitChanges_ReturnsQueueAssignment()
    {
        _git.ModifiedFiles = ["queued.cs", "loose.cs"];
        await EnqueueAsync("queued.cs");

        var result = await _tools.GetCommitChanges(worktreePath: "/repos/app");

        Assert.That(result.Succeeded, Is.True);
        var changes = (CommitChangesResult)result.Data!;
        Assert.That(changes.QueuedFiles, Is.EqualTo(new[] { "queued.cs" }));
        Assert.That(changes.UnassignedFiles, Is.EqualTo(new[] { "loose.cs" }));
    }

    [Test]
    public async Task CommitMetadataTools_UpdateQueuedEntry()
    {
        await EnqueueAsync();

        var message = await _tools.UpdateCommitMessage("1", "fix(s): updated", worktreePath: "/repos/app");
        var files = await _tools.AddCommitFiles("1", ["b.cs"], worktreePath: "/repos/app");

        Assert.That(message.Succeeded, Is.True);
        Assert.That(files.Succeeded, Is.True);
        Assert.That(_queue.Stored!.Commits[0].Message, Is.EqualTo("fix(s): updated"));
        Assert.That(_queue.Stored.Commits[0].Files, Is.EqualTo(new[] { "a.cs", "b.cs" }));
    }

    [Test]
    public async Task CommitArchiveTools_RemoveAndRestoreEntry()
    {
        await EnqueueAsync();
        var entryId = _queue.Stored!.Commits[0].Id;

        var removed = await _tools.RemoveCommit("1", worktreePath: "/repos/app");
        var restored = await _tools.RestoreCommit(entryId, worktreePath: "/repos/app");

        Assert.That(removed.Succeeded, Is.True);
        Assert.That(restored.Succeeded, Is.True);
        Assert.That(_queue.Stored.Commits.Single().Id, Is.EqualTo(entryId));
    }

    [Test]
    public async Task CommitEditTools_BeginAndAbortSession()
    {
        await EnqueueAsync();

        var begin = await _tools.BeginCommitEdit("1", worktreePath: "/repos/app");
        var abort = await _tools.AbortCommitEdit(worktreePath: "/repos/app");

        Assert.That(begin.Succeeded, Is.True);
        Assert.That(abort.Succeeded, Is.True);
        Assert.That(_git.PatchApplied, Is.True);
        Assert.That(_git.FilesRestored, Is.True);
        Assert.That(_queue.Stored!.ActiveSession, Is.Null);
    }

    [Test]
    public async Task InspectCommit_returnsTheQueuedEntry()
    {
        await EnqueueAsync();

        var result = await _tools.InspectCommit("1", includePatch: false, worktreePath: "/repos/app");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(((CommitInspectResult)result.Data!).Entry.Message, Is.EqualTo("feat(s): m"));
        });
    }

    [Test]
    public async Task RemoveCommitFiles_dropsOneFileFromTheQueuedEntry()
    {
        await _tools.EnqueueCommit("feat/s", "feat(s): m", ["a.cs", "b.cs"], worktreePath: "/repos/app");

        var result = await _tools.RemoveCommitFiles("1", ["b.cs"], worktreePath: "/repos/app");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_queue.Stored!.Commits[0].Files, Is.EqualTo(new[] { "a.cs" }));
        });
    }

    [Test]
    public async Task ClearCommits_emptiesTheQueue()
    {
        await EnqueueAsync();

        var result = await _tools.ClearCommits(worktreePath: "/repos/app");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_queue.Stored!.Commits, Is.Empty);
        });
    }

    [Test]
    public async Task SaveCommitEdit_closesTheEditSessionItOpened()
    {
        await EnqueueAsync();
        await _tools.BeginCommitEdit("1", worktreePath: "/repos/app");
        _git.ModifiedFiles = ["a.cs"];

        var result = await _tools.SaveCommitEdit(worktreePath: "/repos/app");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_queue.Stored!.ActiveSession, Is.Null);
        });
    }

    // Every tool here resolves its target through one shared boundary, so the refusal is
    // asserted once across the tools rather than repeated per tool.
    [Test]
    public async Task NoCommitsTool_actsWhenTheCallNamesNoWorkspace()
    {
        var results = new[]
        {
            await _tools.EnqueueCommit("feat/s", "feat(s): m", ["a.cs"]),
            await _tools.EnqueueReviewFixCommit("review-1", "feat/s", "feat(s): m", ["a.cs"]),
            await _tools.GuardCommits(),
            await _tools.GetCommitChanges(),
            await _tools.InspectCommit("1", includePatch: false),
            await _tools.ClearCommits()
        };

        Assert.Multiple(() =>
        {
            foreach (var result in results)
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Message, Does.Contain("Both were omitted"));
            }
        });
    }

    [Test]
    public async Task GetCommitsStatus_readsTheQueueOfARegisteredWorkspaceNamedById()
    {
        var workspace = await _registry.RegisterAsync(
            ServerDomain.Workspace().At("/repos/app").Build());
        await EnqueueAsync();

        var result = await _tools.GetCommitsStatus(workspaceId: workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(((CommitsStatusResult)result.Data!).Entries, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task GetCommitsStatus_refusesAWorkspaceIdAndAWorktreePathTogether()
    {
        var workspace = await _registry.RegisterAsync(
            ServerDomain.Workspace().At("/repos/app").Build());

        var result = await _tools.GetCommitsStatus(workspace.Id, "/repos/app");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not both"));
        });
    }

    [Test]
    public async Task GetCommitsStatus_refusesNeitherAWorkspaceIdNorAWorktreePath()
    {
        var result = await _tools.GetCommitsStatus();

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("Both were omitted"));
        });
    }

    [Test]
    public async Task GetCommitsStatus_namesAnUnregisteredWorkspaceRatherThanAMissingPath()
    {
        var result = await _tools.GetCommitsStatus(workspaceId: "ws-missing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("ws-missing"));
            Assert.That(result.Message, Does.Contain("not registered"));
        });
    }

    [Test]
    public async Task EnqueueCommit_queuesAgainstARegisteredWorkspaceNamedById()
    {
        var workspace = await _registry.RegisterAsync(
            ServerDomain.Workspace().At("/repos/app").Build());

        var result = await _tools.EnqueueCommit(
            "feat/s",
            "feat(s): m",
            ["a.cs"],
            workspaceId: workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(_queue.Stored!.Commits, Has.Count.EqualTo(1));
        });
    }

    // The tool description is the only instruction an agent reads before enqueueing, so each
    // rule it states is asserted on its own.
    [TestCase("Do NOT call git add")]
    [TestCase("git commit")]
    [TestCase("git stash")]
    public void EnqueueCommitDescription_tellsAgentsNotToUseGitDirectly(string instruction)
        => Assert.That(EnqueueCommitDescription(), Does.Contain(instruction));

    [TestCase("scoped to the queued slice")]
    [TestCase("feat is a user-facing addition")]
    [TestCase("test is a test-only or smoke-validation change")]
    [TestCase("chore is maintenance/packaging/CI/tooling")]
    [TestCase("style is CSS/HTML only")]
    [TestCase("docs is documentation only")]
    [TestCase("prompts.commitPolicy")]
    public void EnqueueCommitDescription_statesTheCommitMessageContract(string rule)
        => Assert.That(EnqueueCommitDescription(), Does.Contain(rule));

    [Test]
    public void EnqueueCommitDescription_pointsAgentsAtTheStructuredQueueWorktreePath()
        => Assert.That(EnqueueCommitDescription(), Does.Contain("structured queueWorktreePath"));

    private Task<McpToolResult> EnqueueAsync(string file = "a.cs")
        => _tools.EnqueueCommit("feat/s", "feat(s): m", [file], worktreePath: "/repos/app");

    private static string EnqueueCommitDescription()
        => typeof(CommitQueueMcpTools)
            .GetMethod(nameof(CommitQueueMcpTools.EnqueueCommit))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

    [Test]
    public async Task MockAcpAgent_usesMcpQueuePathAndBuildsOnItsPreviousProposal()
    {
        var queue = new FakeCommitsQueueProvider();
        var proposals = new MockProposalStack();
        var commits = new CommitsService(
            queue,
            new FakeCommitsGitProvider(),
            new CommitPolicyProvider(),
            proposals,
            new EnabledQueueConfiguration(),
            null);
        var tools = new CommitQueueMcpTools(new CommitQueueMcpService(
            new CommitsController(commits),
            ServerTestComposition.CreateWorkspaceTargetController(ServerTestComposition.CreateRegistry())));
        var mcp = new MockCommitQueueMcpClient(tools);
        var acp = new MockAcpAgent(mcp, "/repos/app");

        await acp.CompleteTaskAsync("feat/queue", "feat(queue): add base", "shared.cs");
        await acp.StartNextTaskAsync();
        await acp.CompleteTaskAsync("feat/queue", "feat(queue): extend base", "shared.cs");
        var status = await mcp.StatusAsync(acp.WorktreePath);

        Assert.Multiple(() =>
        {
            Assert.That(acp.WorktreePath, Is.EqualTo("/managed/queue"));
            Assert.That(proposals.ReceivedWorktrees, Is.EqualTo(new[] { "/repos/app", "/managed/queue" }));
            Assert.That(status.Generation, Is.EqualTo(2));
            Assert.That(status.Entries, Has.Count.EqualTo(2));
            Assert.That(status.Entries[1].ParentCommit, Is.EqualTo(status.Entries[0].ProposalCommit));
            Assert.That(status.Entries[0].Files, Is.EqualTo(status.Entries[1].Files));
        });
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;
        public Dictionary<string, string> Patches { get; } = [];

        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
        {
            Patches[patchKey] = patch;
            return Task.CompletedTask;
        }

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
        {
            Patches.Remove(patchKey);
            return Task.CompletedTask;
        }

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Patches.GetValueOrDefault(patchKey));

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class EnabledQueueConfiguration : ICommitQueueConfigurationProvider
    {
        public bool IsGitQueueEnabled(string worktreePath) => true;
    }

    private sealed class MockProposalStack : IProposalStackGitProvider
    {
        public List<string> ReceivedWorktrees { get; } = [];

        public Task<ProposalCommitResult> EnqueueAsync(
            string worktreePath,
            CommitsQueue current,
            string queueId,
            string message,
            IReadOnlyList<string> files,
            CancellationToken cancellationToken = default)
        {
            ReceivedWorktrees.Add(worktreePath);
            var parent = current.TipCommit ?? "base";
            var commit = $"proposal-{current.Generation + 1}";
            return Task.FromResult(new ProposalCommitResult("base", parent, commit, "/managed/queue", $"refs/agent-up/queues/{queueId}/tip", "patch"));
        }
    }

    private sealed class MockCommitQueueMcpClient(CommitQueueMcpTools tools)
    {
        public Task<McpToolResult> EnqueueAsync(string worktree, string slice, string message, string file)
            => tools.EnqueueCommit(slice, message, [file], worktreePath: worktree);

        public Task<McpToolResult> GuardAsync(string worktree)
            => tools.GuardCommits(worktreePath: worktree);

        public async Task<CommitsStatusResult> StatusAsync(string worktree)
        {
            var result = await tools.GetCommitsStatus(worktreePath: worktree);
            return (CommitsStatusResult)result.Data!;
        }
    }

    private sealed class MockAcpAgent(MockCommitQueueMcpClient mcp, string worktreePath)
    {
        public string WorktreePath { get; private set; } = worktreePath;

        public async Task CompleteTaskAsync(string slice, string message, string file)
        {
            var result = await mcp.EnqueueAsync(WorktreePath, slice, message, file);
            Assert.That(result.Succeeded, Is.True, result.Message);
            WorktreePath = ((CommitsEnqueueResult)result.Data!).QueueWorktreePath ?? WorktreePath;
        }

        public async Task StartNextTaskAsync()
        {
            var result = await mcp.GuardAsync(WorktreePath);
            Assert.That(result.Succeeded, Is.True, result.Message);
            WorktreePath = ((CommitGuardResult)result.Data!).ContinueWorktreePath ?? WorktreePath;
        }
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public string[] ModifiedFiles { get; set; } = [];
        public GitOperationState OperationState { get; set; } = GitOperationState.None;
        public bool PatchApplied { get; private set; }
        public bool FilesRestored { get; private set; }

        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(ModifiedFiles);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult("diff --git a/a.cs b/a.cs\n");

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationState);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
        {
            PatchApplied = true;
            return Task.CompletedTask;
        }

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            FilesRestored = true;
            return Task.CompletedTask;
        }
    }
}
