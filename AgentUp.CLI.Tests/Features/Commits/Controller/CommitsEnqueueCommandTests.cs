using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Providers;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitsEnqueueCommandTests
{
    [Test]
    public async Task RunAsync_missingSlice_writesErrorAndReturnsOne()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--message", "msg", "--files", "a.cs"]);

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--slice"));
    }

    [Test]
    public async Task RunAsync_missingMessage_writesErrorAndReturnsOne()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--slice", "S", "--files", "a.cs"]);

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--message"));
    }

    [Test]
    public async Task RunAsync_missingFiles_writesErrorAndReturnsOne()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--slice", "S", "--message", "msg"]);

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--files"));
    }

    [Test]
    public async Task RunAsync_unknownFlag_writesErrorAndReturnsOne()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--unknown", "val"]);

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Unknown argument"));
    }

    [Test]
    public async Task RunAsync_validArgs_returnsZeroAndWritesConfirmation()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--slice", "MySlice", "--message", "feat: thing", "--files", "a.cs"]);

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("MySlice"));
    }

    [Test]
    public async Task RunAsync_validArgs_enqueuesToService()
    {
        using var output = new StringWriter();
        var queue = new FakeCommitsQueueProvider();
        var command = BuildCommand(output, queue);

        await command.RunAsync(["--slice", "MySlice", "--message", "feat: thing", "--files", "a.cs", "b.cs"]);

        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(1));
        Assert.That(queue.Stored.Commits[0].Slice, Is.EqualTo("MySlice"));
        Assert.That(queue.Stored.Commits[0].Files, Is.EqualTo(new[] { "a.cs", "b.cs" }));
    }

    [Test]
    public async Task RunAsync_validArgs_outputIncludesTotalCount()
    {
        using var output = new StringWriter();
        var existing = new CommitsQueue(1, [new CommitEntry("First", "fix: first", ["x.cs"], [])]);
        var command = BuildCommand(output, new FakeCommitsQueueProvider(existing));

        await command.RunAsync(["--slice", "Second", "--message", "feat: second", "--files", "y.cs"]);

        Assert.That(output.ToString(), Does.Contain("2"));
    }

    [Test]
    public async Task RunAsync_withTestsFlag_parsesTestsIntoEntry()
    {
        using var output = new StringWriter();
        var queue = new FakeCommitsQueueProvider();
        var command = BuildCommand(output, queue);

        await command.RunAsync(["--slice", "S", "--message", "msg", "--files", "a.cs", "--tests", "dotnet test Foo"]);

        Assert.That(queue.Stored!.Commits[0].Tests, Is.EqualTo(new[] { "dotnet test Foo" }));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CommitsEnqueueCommand BuildCommand(StringWriter output, FakeCommitsQueueProvider? queue = null)
    {
        var provider = queue ?? new FakeCommitsQueueProvider();
        var service = new CommitsService(provider, new FakeCommitsGitProvider());
        return new CommitsEnqueueCommand(service, new CommitsArgParser(), new CommitsOutputService(output));
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;

        public Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            Stored = null;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string slice, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetStagingAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
