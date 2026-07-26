using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controllers;

[TestFixture]
public sealed class CommitsNextCommandTests
{
    [Test]
    public async Task RunAsync_emptyQueue_returnsZeroWithMessage()
    {
        var (command, _, output) = Build();

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("empty").Or.Contain("Empty"));
    }

    [Test]
    public async Task RunAsync_singleEntry_stagesFilesAndReturnsZero()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs", "b.cs"], []);
        var (command, git, output) = Build(new CommitsQueue(1, [entry]));

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(git.StagedFiles, Is.EqualTo(new[] { "a.cs", "b.cs" }));
    }

    [Test]
    public async Task RunAsync_singleEntry_outputContainsSliceAndMessage()
    {
        var entry = new CommitEntry("MySlice", "feat: msg", ["a.cs"], []);
        var (command, _, output) = Build(new CommitsQueue(1, [entry]));

        await command.RunAsync();

        var text = output.ToString();
        Assert.That(text, Does.Contain("MySlice"));
        Assert.That(text, Does.Contain("feat: msg"));
    }

    [Test]
    public async Task RunAsync_singleEntry_outputSuggestsGitCommitCommand()
    {
        var entry = new CommitEntry("Slice", "feat: the feature", ["a.cs"], []);
        var (command, _, output) = Build(new CommitsQueue(1, [entry]));

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
        var (command, _, output) = Build(queue);

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
        var (command, git, _) = Build(queue);

        await command.RunAsync();

        Assert.That(git.StagedFiles, Does.Not.Contain("b.cs"));
    }

    [Test]
    public async Task RunAsync_lastEntry_outputIndicatesQueueIsEmpty()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        var (command, _, output) = Build(new CommitsQueue(1, [entry]));

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("empty"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (CommitsNextCommand Command, FakeCommitsGitProvider Git, StringWriter Output) Build(
        CommitsQueue? queue = null)
    {
        var output = new StringWriter();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(new FakeCommitsQueueProvider(queue), git);
        return (new CommitsNextCommand(service, new CommitsOutputService(output)), git, output);
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
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public List<string> StagedFiles { get; } = [];

        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            StagedFiles.AddRange(files);
            return Task.CompletedTask;
        }
    }
}
