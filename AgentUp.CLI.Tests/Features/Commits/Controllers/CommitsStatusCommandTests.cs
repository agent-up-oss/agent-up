using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controllers;

[TestFixture]
public sealed class CommitsStatusCommandTests
{
    [Test]
    public async Task RunAsync_emptyQueue_writesNoCommitsMessage()
    {
        var (command, output) = Build();

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No commits queued"));
    }

    [Test]
    public async Task RunAsync_singleEntry_writesSliceAndMessage()
    {
        var queue = new CommitsQueue(1, [new CommitEntry("MySlice", "feat: thing", ["a.cs"], [])]);
        var (command, output) = Build(queue: queue);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("MySlice"));
        Assert.That(output.ToString(), Does.Contain("feat: thing"));
    }

    [Test]
    public async Task RunAsync_multipleEntries_listsAll()
    {
        var queue = new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], []),
            new CommitEntry("Second", "fix: second", ["b.cs"], [])
        ]);
        var (command, output) = Build(queue: queue);

        await command.RunAsync();

        var text = output.ToString();
        Assert.That(text, Does.Contain("First"));
        Assert.That(text, Does.Contain("Second"));
        Assert.That(text, Does.Contain("[1]"));
        Assert.That(text, Does.Contain("[2]"));
    }

    [Test]
    public async Task RunAsync_unassignedFiles_writesWarning()
    {
        var queue = new CommitsQueue(1, [new CommitEntry("Slice", "msg", ["owned.cs"], [])]);
        var (command, output) = Build(queue: queue, modifiedFiles: ["owned.cs", "unassigned.cs"]);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("Warning"));
        Assert.That(output.ToString(), Does.Contain("unassigned.cs"));
    }

    [Test]
    public async Task RunAsync_allFilesAssigned_noWarning()
    {
        var queue = new CommitsQueue(1, [new CommitEntry("Slice", "msg", ["a.cs", "b.cs"], [])]);
        var (command, output) = Build(queue: queue, modifiedFiles: ["a.cs", "b.cs"]);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Not.Contain("Warning"));
    }

    [Test]
    public async Task RunAsync_entryWithTests_showsTestCount()
    {
        var queue = new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["a.cs"], ["dotnet test Foo", "dotnet test Bar"])
        ]);
        var (command, output) = Build(queue: queue);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("2 test command(s)"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static (CommitsStatusCommand Command, StringWriter Output) Build(
        CommitsQueue? queue = null,
        string[]? modifiedFiles = null)
    {
        var output = new StringWriter();
        var service = new CommitsService(
            new FakeCommitsQueueProvider(queue),
            new FakeCommitsGitProvider(modifiedFiles ?? []));
        return (new CommitsStatusCommand(service, new CommitsOutputService(output)), output);
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(initial ?? CommitsQueue.Empty());

        public Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCommitsGitProvider(params string[] modifiedFiles) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(modifiedFiles);

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
