using System.Diagnostics;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Providers;

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
        });
        Assert.That(await GitAsync(repository, "rev-parse", "HEAD"), Is.EqualTo(humanHead));

        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "second\n");
        var state = new CommitsQueue(
            3,
            [new CommitEntry("Test", "feat(Test): first", ["value.txt"], ProposalCommit: first.Commit)],
            QueueId: "queue-1",
            BaseCommit: first.BaseCommit,
            TipCommit: first.Commit,
            QueueWorktreePath: first.QueueWorktreePath,
            Generation: 1);
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
    public async Task EnqueueAsync_rejectsAnEmptyPatch()
    {
        var git = new ScriptedGitProvider { Diff = "  " };
        var provider = new ProposalStackGitProvider(git);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync("/repo", CommitsQueue.Empty(), "queue-1", "feat(Test): empty", ["a.cs"]));

        Assert.That(exception!.Message, Does.Contain("no changes to enqueue"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsALegacyQueueWithoutABaseCommit()
    {
        var git = new ScriptedGitProvider { Diff = "diff --git a/a.cs b/a.cs\n" };
        var provider = new ProposalStackGitProvider(git);
        var current = new CommitsQueue(3, [new CommitEntry("Test", "feat(Test): first", ["a.cs"])], QueueId: "queue-1");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync("/repo", current, "queue-1", "feat(Test): second", ["a.cs"]));

        Assert.That(exception!.Message, Does.Contain("no base commit"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAQueueWithoutATipCommit()
    {
        var git = new ScriptedGitProvider { Diff = "diff --git a/a.cs b/a.cs\n" };
        var provider = new ProposalStackGitProvider(git);
        var current = new CommitsQueue(
            3,
            [new CommitEntry("Test", "feat(Test): first", ["a.cs"])],
            QueueId: "queue-1",
            BaseCommit: "base");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync("/repo", current, "queue-1", "feat(Test): second", ["a.cs"]));

        Assert.That(exception!.Message, Does.Contain("no tip commit"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAQueueWithoutAManagedWorktree()
    {
        var git = new ScriptedGitProvider { Diff = "diff --git a/a.cs b/a.cs\n" };
        var provider = new ProposalStackGitProvider(git);
        var current = new CommitsQueue(
            3,
            [new CommitEntry("Test", "feat(Test): first", ["a.cs"])],
            QueueId: "queue-1",
            BaseCommit: "base",
            TipCommit: "tip");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync("/repo", current, "queue-1", "feat(Test): second", ["a.cs"]));

        Assert.That(exception!.Message, Does.Contain("no managed worktree"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAPreexistingQueueWorktree()
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

        var git = new CommitsGitProvider();
        var provider = new ProposalStackGitProvider(git, storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        Directory.CreateDirectory(first.QueueWorktreePath.Replace("queue-1", "queue-2"));
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "again\n");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-2", "feat(Test): second", ["value.txt"]));

        Assert.That(exception!.Message, Does.Contain("worktree already exists"));
    }

    [Test]
    public async Task EnqueueAsync_requiresDependentWorkInTheManagedWorktree()
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

        var git = new CommitsGitProvider();
        var provider = new ProposalStackGitProvider(git, storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        var state = new CommitsQueue(
            3,
            [new CommitEntry("Test", "feat(Test): first", ["value.txt"], ProposalCommit: first.Commit)],
            QueueId: "queue-1",
            BaseCommit: first.BaseCommit,
            TipCommit: first.Commit,
            QueueWorktreePath: first.QueueWorktreePath,
            Generation: 1);
        await File.WriteAllTextAsync(Path.Join(repository, "value.txt"), "second\n");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, state, "queue-1", "feat(Test): second", ["value.txt"]));

        Assert.That(exception!.Message, Does.Contain("managed queue worktree"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsAWorktreeThatDriftedFromTheTip()
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

        var git = new CommitsGitProvider();
        var provider = new ProposalStackGitProvider(git, storage);
        var first = await provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first", ["value.txt"]);
        await GitAsync(first.QueueWorktreePath, "reset", "--hard", first.BaseCommit);
        await File.WriteAllTextAsync(Path.Join(first.QueueWorktreePath, "value.txt"), "drifted\n");
        var state = new CommitsQueue(
            3,
            [new CommitEntry("Test", "feat(Test): first", ["value.txt"], ProposalCommit: first.Commit)],
            QueueId: "queue-1",
            BaseCommit: first.BaseCommit,
            TipCommit: first.Commit,
            QueueWorktreePath: first.QueueWorktreePath,
            Generation: 1);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(first.QueueWorktreePath, state, "queue-1", "feat(Test): second", ["value.txt"]));

        Assert.That(exception!.Message, Does.Contain("no longer matches the recorded queue tip"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsLiteralBreakingGitArguments()
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

        var provider = new ProposalStackGitProvider(new CommitsGitProvider(), storage);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): first\nextra", ["value.txt"]));

        Assert.That(exception!.Message, Does.Contain("literal values"));
    }

    [Test]
    public async Task EnqueueAsync_removesAFailedFirstWorktreeWhenThePatchCannotApply()
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

        var git = new ScriptedGitProvider
        {
            RepoRoot = repository,
            Diff = "diff --git a/missing.cs b/missing.cs\nindex 1111111..2222222 100644\n--- a/missing.cs\n+++ b/missing.cs\n@@ -1 +1 @@\n-old\n+new\n"
        };
        var provider = new ProposalStackGitProvider(git, storage);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EnqueueAsync(repository, CommitsQueue.Empty(), "queue-1", "feat(Test): broken", ["value.txt"]));

        Assert.That(exception!.Message, Does.Contain("Could not apply the proposal patch"));
        Assert.That(Directory.Exists(Path.Join(storage, "agentup", "commits", "worktrees")), Is.True);
        Assert.That(
            Directory.GetDirectories(Path.Join(storage, "agentup", "commits", "worktrees"), "*", SearchOption.AllDirectories)
                .Any(path => path.EndsWith("queue-1", StringComparison.Ordinal)),
            Is.False);
    }

    [Test]
    public async Task EnqueueAsync_removesTheFailedWorktreeWhenRestoreThrows()
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
        File.WriteAllText(Path.Join(root, "agent-up.json"), "{");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new CommitQueueConfigurationProvider().IsGitQueueEnabled(root));

        Assert.That(exception!.Message, Does.Contain("not valid JSON"));
        Assert.That(exception.InnerException, Is.InstanceOf<System.Text.Json.JsonException>());
    }

    [Test]
    public void CommitsQueueJson_roundTripsArchivedEntries()
    {
        var archived = new ArchivedCommitEntry(
            new CommitEntry("Commits", "feat(Commits): queue", ["a.cs"], "entry-1", "patch-1"),
            "2026-09-13T00:00:00Z");
        var queue = new CommitsQueue(3, [], Archive: [archived], QueueId: "queue-1", BaseCommit: "base", TipCommit: "tip", QueueWorktreePath: "/managed", Generation: 2);

        var restored = CommitsQueueJson.FromModel(queue).ToModel();

        Assert.Multiple(() =>
        {
            Assert.That(restored.Archived, Has.Count.EqualTo(1));
            Assert.That(restored.Archived[0].Entry.Id, Is.EqualTo("entry-1"));
            Assert.That(restored.QueueId, Is.EqualTo("queue-1"));
            Assert.That(restored.Generation, Is.EqualTo(2));
        });
    }

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

    private sealed class ScriptedGitProvider : ICommitsGitProvider
    {
        public string RepoRoot { get; init; } = "/repo";
        public string Diff { get; init; } = "diff --git a/a.cs b/a.cs\n";

        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(RepoRoot);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(Diff);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RestoreFailingGit(ICommitsGitProvider inner, Exception error) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetRepoRootAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetModifiedFilesAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetStagedFilesAsync(worktreePath, cancellationToken);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetUntrackedFilesAsync(worktreePath, cancellationToken);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => inner.GetDiffAsync(worktreePath, files, cancellationToken);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.HasStagedChangesAsync(worktreePath, cancellationToken);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => inner.GetOperationStateAsync(worktreePath, cancellationToken);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => inner.ApplyPatchAsync(worktreePath, patch, cancellationToken);

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => throw error;
    }
}
