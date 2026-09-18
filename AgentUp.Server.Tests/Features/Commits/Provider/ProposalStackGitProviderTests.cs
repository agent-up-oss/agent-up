using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Providers;
using AgentUp.Server.Tests.Support;
using System.Diagnostics;

namespace AgentUp.Server.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class ProposalStackGitProviderTests
{
    private string? root;

    [TearDown]
    public void TearDown()
    {
        if (root is not null && Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task EnqueueAsync_createsDependentCommitsWithoutMovingHumanBranch()
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var repository = Path.Join(root, "repository");
        var storage = Path.Join(root, "storage");
        Directory.CreateDirectory(repository);
        await GitAsync(repository, "init");
        await GitAsync(repository, "config", "user.name", "Test User");
        await GitAsync(repository, "config", "user.email", "test@example.invalid");
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "base\n");
        await GitAsync(repository, "add", "value.txt");
        await GitAsync(repository, "commit", "-m", "base");
        var humanHead = await GitAsync(repository, "rev-parse", "HEAD");

        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "first\n");
        var git = new CommitsGitProvider();
        var provider = new ProposalStackGitProvider(git, storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);

        Assert.Multiple(() =>
        {
            Assert.That(first.ParentCommit, Is.EqualTo(humanHead));
            Assert.That(File.ReadAllText(Path.Join(repository, "value.txt")), Is.EqualTo("base\n"));
            Assert.That(Directory.Exists(first.QueueWorktreePath), Is.True);
            Assert.That(first.QueueRef, Is.EqualTo("refs/agent-up/queues/queue-1/tip"));
        });
        Assert.That(await GitAsync(repository, "rev-parse", "HEAD"), Is.EqualTo(humanHead));

        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "second\n");
        var state = ServerDomain.Queue()
            .AtVersion(3)
            .With(ServerDomain.CommitEntry()
                .For("Test")
                .Saying("feat(Test): first")
                .Touching(["value.txt"])
                .WithProposalCommit(first.Commit)
                .Build())
            .WithQueueId("queue-1")
            .WithBaseCommit(first.BaseCommit)
            .WithTipCommit(first.Commit)
            .InWorktree(first.QueueWorktreePath)
            .AtGeneration(1)
            .Build();
        var second = await provider.EnqueueAsync(first.QueueWorktreePath, state, "queue-1", "feat(Test): second", ["value.txt"]);

        Assert.Multiple(() =>
        {
            Assert.That(second.ParentCommit, Is.EqualTo(first.Commit));
            Assert.That(second.Commit, Is.Not.EqualTo(first.Commit));
            Assert.That(File.ReadAllText(Path.Join(second.QueueWorktreePath, "value.txt")), Is.EqualTo("second\n"));
        });
        Assert.That(await GitAsync(repository, "rev-parse", "refs/agent-up/queues/queue-1/tip"), Is.EqualTo(second.Commit));
        Assert.That(await GitAsync(repository, "rev-parse", "HEAD"), Is.EqualTo(humanHead));
    }

    [Test]
    public async Task EnqueueAsync_removesTheFailedWorktreeAndRefWhenRestoreThrows()
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var repository = Path.Join(root, "repository");
        var storage = Path.Join(root, "storage");
        Directory.CreateDirectory(repository);
        await GitAsync(repository, "init");
        await GitAsync(repository, "config", "user.name", "Test User");
        await GitAsync(repository, "config", "user.email", "test@example.invalid");
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "base\n");
        await GitAsync(repository, "add", "value.txt");
        await GitAsync(repository, "commit", "-m", "base");
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "first\n");

        var git = new RestoreFailingGit(new CommitsGitProvider(), new IOException("restore failed"));
        var provider = new ProposalStackGitProvider(git, storage);

        Assert.ThrowsAsync<IOException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]));

        var refs = await GitAsync(repository, "for-each-ref", "refs/agent-up/queues/queue-1/tip");
        Assert.That(refs, Is.Empty);
        var worktrees = Path.Join(storage, "agentup", "commits", "worktrees");
        Assert.That(
            !Directory.Exists(worktrees)
            || !Directory.GetDirectories(worktrees, "*", SearchOption.AllDirectories)
                .Any(path => path.EndsWith("queue-1", StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public void CommitQueueConfigurationProvider_requiresExplicitOptIn()
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var provider = new CommitQueueConfigurationProvider();

        Assert.That(provider.IsGitQueueEnabled(root), Is.False);
        File.WriteAllText(Path.Join(root, "agent-up.json"), """{"commits":{"enabled":true}}""");
        Assert.That(provider.IsGitQueueEnabled(root), Is.True);
    }

    [Test]
    public void CommitQueueConfigurationProvider_rejectsMalformedJson()
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.json"), "{not-json");

        var error = Assert.Throws<InvalidOperationException>(
            () => new CommitQueueConfigurationProvider().IsGitQueueEnabled(root));
        Assert.That(error!.Message, Does.Contain("not valid JSON"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAnEmptyPatch()
    {
        var (repository, storage) = await InitRepositoryAsync();
        var git = new RestoreFailingGit(new CommitsGitProvider(), restoreError: null, diff: " ");
        var provider = new ProposalStackGitProvider(git, storage);

        var error = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): empty", ["value.txt"]));
        Assert.That(error!.Message, Does.Contain("no changes to enqueue"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAMessageWithControlCharacters()
    {
        var (repository, storage) = await InitRepositoryAsync("changed\n");
        var provider = new ProposalStackGitProvider(new CommitsGitProvider(), storage);

        var error = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test):\nfirst", ["value.txt"]));
        Assert.That(error!.Message, Does.Contain("literal values"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsASecondFirstEntryWhenTheWorktreeAlreadyExists()
    {
        var (repository, storage) = await InitRepositoryAsync("first\n");
        var provider = new ProposalStackGitProvider(new CommitsGitProvider(), storage);
        await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "again\n");

        var error = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): again", ["value.txt"]));
        Assert.That(error!.Message, Does.Contain("worktree already exists"));
    }

    [Test]
    public async Task EnqueueAsync_requiresTheManagedWorktreeForLaterEntries()
    {
        var (repository, storage) = await InitRepositoryAsync("first\n");
        var provider = new ProposalStackGitProvider(new CommitsGitProvider(), storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        var state = Queued(first);
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "second\n");
        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "second\n");

        var wrongRoot = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, state, "queue-1", "feat(Test): second", ["value.txt"]));
        Assert.That(wrongRoot!.Message, Does.Contain("managed queue worktree"));

        var missingBase = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(first.QueueWorktreePath, state with { BaseCommit = null }, "queue-1", "feat(Test): second", ["value.txt"]));
        Assert.That(missingBase!.Message, Does.Contain("no base commit"));

        var missingTip = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(first.QueueWorktreePath, state with { TipCommit = null }, "queue-1", "feat(Test): second", ["value.txt"]));
        Assert.That(missingTip!.Message, Does.Contain("no tip commit"));

        var missingWorktree = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(first.QueueWorktreePath, state with { QueueWorktreePath = null }, "queue-1", "feat(Test): second", ["value.txt"]));
        Assert.That(missingWorktree!.Message, Does.Contain("no managed worktree"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAQueueWorktreeThatDriftedFromTheTip()
    {
        var (repository, storage) = await InitRepositoryAsync("first\n");
        var provider = new ProposalStackGitProvider(new CommitsGitProvider(), storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "drift\n");
        await GitAsync(first.QueueWorktreePath, "add", "value.txt");
        await GitAsync(first.QueueWorktreePath, "commit", "--no-verify", "-m", "drift");
        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "second\n");

        var error = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(first.QueueWorktreePath, Queued(first), "queue-1", "feat(Test): second", ["value.txt"]));
        Assert.That(error!.Message, Does.Contain("no longer matches the recorded queue tip"));
    }

    [Test]
    public async Task EnqueueAsync_removesTheFailedWorktreeWhenRestoreThrowsInvalidOperation()
    {
        var (repository, storage) = await InitRepositoryAsync("first\n");
        var git = new RestoreFailingGit(new CommitsGitProvider(), new InvalidOperationException("restore failed"));
        var provider = new ProposalStackGitProvider(git, storage);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]));
        Assert.That(await GitAsync(repository, "for-each-ref", "refs/agent-up/queues/queue-1/tip"), Is.Empty);
    }

    [Test]
    public async Task EnqueueAsync_removesTheFailedWorktreeWhenRestoreIsCanceled()
    {
        var (repository, storage) = await InitRepositoryAsync("first\n");
        var git = new RestoreFailingGit(new CommitsGitProvider(), new OperationCanceledException());
        var provider = new ProposalStackGitProvider(git, storage);

        Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]));
        Assert.That(await GitAsync(repository, "for-each-ref", "refs/agent-up/queues/queue-1/tip"), Is.Empty);
    }

    [Test]
    public async Task EnqueueAsync_rejectsAPatchGitCannotApply()
    {
        var (repository, storage) = await InitRepositoryAsync();
        var git = new RestoreFailingGit(new CommitsGitProvider(), restoreError: null, diff: "this is not a git patch\n");
        var provider = new ProposalStackGitProvider(git, storage);

        var error = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]));
        Assert.That(error!.Message, Does.Contain("Could not apply the proposal patch"));
        Assert.That(await GitAsync(repository, "for-each-ref", "refs/agent-up/queues/queue-1/tip"), Is.Empty);
    }

    private async Task<(string Repository, string Storage)> InitRepositoryAsync(string? dirtyContents = null)
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var repository = Path.Join(root, "repository");
        var storage = Path.Join(root, "storage");
        Directory.CreateDirectory(repository);
        await GitAsync(repository, "init");
        await GitAsync(repository, "config", "user.name", "Test User");
        await GitAsync(repository, "config", "user.email", "test@example.invalid");
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "base\n");
        await GitAsync(repository, "add", "value.txt");
        await GitAsync(repository, "commit", "-m", "base");
        if (dirtyContents is not null)
            await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), dirtyContents);
        return (repository, storage);
    }

    private static CommitsQueue Queued(ProposalCommitResult first)
        => new(
            3,
            [ServerDomain.CommitEntry()
                .For("Test")
                .Saying("feat(Test): first")
                .Touching(["value.txt"])
                .WithProposalCommit(first.Commit)
                .Build()],
            QueueId: "queue-1",
            BaseCommit: first.BaseCommit,
            TipCommit: first.Commit,
            QueueWorktreePath: first.QueueWorktreePath,
            Generation: 1);

    private static async Task<string> GitAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Git.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(error);
        return output.Trim();
    }

    private sealed class RestoreFailingGit(ICommitsGitProvider inner, Exception? restoreError, string? diff = null) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetRepoRootAsync(worktreePath, cancellationToken);

        public Task<string> GetRepositoryIdentityAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetRepositoryIdentityAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetModifiedFilesAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetStagedFilesAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetUntrackedFilesAsync(worktreePath, cancellationToken);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => diff is null
                ? inner.GetDiffAsync(worktreePath, files, cancellationToken)
                : Task.FromResult(diff);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.HasStagedChangesAsync(worktreePath, cancellationToken);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetOperationStateAsync(worktreePath, cancellationToken);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => inner.ApplyPatchAsync(worktreePath, patch, cancellationToken);

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => restoreError is null
                ? inner.RestoreFilesAsync(worktreePath, files, cancellationToken)
                : throw restoreError;
    }
}
