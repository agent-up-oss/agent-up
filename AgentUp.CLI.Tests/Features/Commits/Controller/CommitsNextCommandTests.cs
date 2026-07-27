using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitsNextCommandTests
{
    [Test]
    public async Task RunAsync_emptyQueue_returnsZeroWithMessage()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("empty").Or.Contain("Empty"));
    }

    [Test]
    public async Task RunAsync_singleEntry_stagesFilesAndReturnsZero()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs", "b.cs"], []);
        var git = new FakeCommitsGitProvider();
        using var output = new StringWriter();
        var command = BuildCommand(output, new CommitsQueue(1, [entry]), git);

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(git.StagedFiles, Is.EqualTo(new[] { "a.cs", "b.cs" }));
    }

    [Test]
    public async Task RunAsync_singleEntry_outputContainsSliceAndMessage()
    {
        var entry = new CommitEntry("MySlice", "feat: msg", ["a.cs"], []);
        using var output = new StringWriter();
        var command = BuildCommand(output, new CommitsQueue(1, [entry]));

        await command.RunAsync();

        var text = output.ToString();
        Assert.That(text, Does.Contain("MySlice"));
        Assert.That(text, Does.Contain("feat: msg"));
    }

    [Test]
    public async Task RunAsync_singleEntry_outputSuggestsGitCommitCommand()
    {
        var entry = new CommitEntry("Slice", "feat: the feature", ["a.cs"], []);
        using var output = new StringWriter();
        var command = BuildCommand(output, new CommitsQueue(1, [entry]));

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("git commit -m \"feat: the feature\""));
    }

    [Test]
    public async Task RunAsync_multipleEntries_popsFirstAndShowsRemaining()
    {
        var queue = new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], []),
            new CommitEntry("Second", "fix: second", ["b.cs"], [])
        ]);
        using var output = new StringWriter();
        var command = BuildCommand(output, queue);

        await command.RunAsync();

        var text = output.ToString();
        Assert.That(text, Does.Contain("First"));
        Assert.That(text, Does.Contain("1").And.Contain("remaining"));
    }

    [Test]
    public async Task RunAsync_multipleEntries_doesNotStageSecondEntryFiles()
    {
        var queue = new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], []),
            new CommitEntry("Second", "fix: second", ["b.cs"], [])
        ]);
        var git = new FakeCommitsGitProvider();
        using var output = new StringWriter();
        var command = BuildCommand(output, queue, git);

        await command.RunAsync();

        Assert.That(git.StagedFiles, Does.Not.Contain("b.cs"));
    }

    [Test]
    public async Task RunAsync_lastEntry_outputIndicatesQueueIsEmpty()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        using var output = new StringWriter();
        var command = BuildCommand(output, new CommitsQueue(1, [entry]));

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("empty"));
    }

    [Test]
    public async Task RunAsync_whenStagedChangesExist_returnsOneWithError()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        var git = new FakeCommitsGitProvider(hasStagedChanges: true);
        var command = BuildCommand(output, new CommitsQueue(1, [entry]), git);

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("not yet committed"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CommitsNextCommand BuildCommand(
        StringWriter output,
        CommitsQueue? queue = null,
        FakeCommitsGitProvider? git = null)
    {
        var gitProvider = git ?? new FakeCommitsGitProvider();
        var service = new CommitsService(new FakeCommitsQueueProvider(queue), gitProvider);
        return new CommitsNextCommand(service, new CommitsOutputService(output));
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        private CommitsQueue? _stored = initial;

        public Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_stored ?? CommitsQueue.Empty());

        public Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            _stored = queue;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            _stored = null;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string slice, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string slice, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeCommitsGitProvider(bool hasStagedChanges = false) : ICommitsGitProvider
    {
        public List<string> StagedFiles { get; } = [];

        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(hasStagedChanges);

        public Task ApplyPatchAsync(string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            StagedFiles.AddRange(files);
            return Task.CompletedTask;
        }

        public Task ResetStagingAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
