using System.Diagnostics;
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
    public void CommitQueueConfigurationProvider_requiresExplicitOptIn()
    {
        root = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var provider = new CommitQueueConfigurationProvider();

        Assert.That(provider.IsGitQueueEnabled(root), Is.False);
        File.WriteAllText(Path.Join(root, "agent-up.json"), """{"commits":{"enabled":true}}""");
        Assert.That(provider.IsGitQueueEnabled(root), Is.True);
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
}
