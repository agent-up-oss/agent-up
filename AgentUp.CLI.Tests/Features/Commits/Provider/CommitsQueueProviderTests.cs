using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Providers;

namespace AgentUp.CLI.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class CommitsQueueProviderTests
{
    private string _storageRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _storageRoot = Path.Join(Path.GetTempPath(), "AgentUp-CommitsQueueProviderTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_storageRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    [Test]
    public async Task WithLockAsync_waitsForConcurrentLockHolder()
    {
        var provider = new CommitsQueueProvider(new FakeCommitsGitProvider(), _storageRoot);
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var secondRan = false;

        var first = provider.WithLockAsync(async ct =>
        {
            firstStarted.Set();
            await Task.Run(() => releaseFirst.Wait(ct), ct);
            return true;
        });
        Assert.That(firstStarted.Wait(TimeSpan.FromSeconds(1)), Is.True);

        var second = provider.WithLockAsync(ct =>
        {
            secondRan = true;
            return Task.FromResult(true);
        });
        await Task.Delay(100);

        Assert.That(secondRan, Is.False);
        releaseFirst.Set();
        await Task.WhenAll(first, second);
        Assert.That(secondRan, Is.True);
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult("");

        public Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GitOperationState.None);

        public Task ApplyPatchAsync(string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetStagingAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
