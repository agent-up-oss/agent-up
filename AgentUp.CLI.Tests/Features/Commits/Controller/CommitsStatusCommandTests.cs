using System.Text.Json;
using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Providers;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitsStatusCommandTests
{
    [Test]
    public async Task RunAsync_emptyQueue_writesNoCommitsMessage()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync();

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("No commits queued"));
    }

    [Test]
    public async Task RunAsync_singleEntry_writesSliceAndMessage()
    {
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [new CommitEntry("MySlice", "feat: thing", ["a.cs"], [])]);
        var command = BuildCommand(output, queue: queue);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("MySlice"));
        Assert.That(output.ToString(), Does.Contain("feat: thing"));
    }

    [Test]
    public async Task RunAsync_multipleEntries_listsAll()
    {
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], []),
            new CommitEntry("Second", "fix: second", ["b.cs"], [])
        ]);
        var command = BuildCommand(output, queue: queue);

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
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [new CommitEntry("Slice", "msg", ["owned.cs"], [])]);
        var command = BuildCommand(output, queue: queue, modifiedFiles: ["owned.cs", "unassigned.cs"]);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("Warning"));
        Assert.That(output.ToString(), Does.Contain("unassigned.cs"));
    }

    [Test]
    public async Task RunAsync_allFilesAssigned_noWarning()
    {
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [new CommitEntry("Slice", "msg", ["a.cs", "b.cs"], [])]);
        var command = BuildCommand(output, queue: queue, modifiedFiles: ["a.cs", "b.cs"]);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Not.Contain("Warning"));
    }

    [Test]
    public async Task RunAsync_entryWithTests_showsTestCount()
    {
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["a.cs"], ["dotnet test Foo", "dotnet test Bar"])
        ]);
        var command = BuildCommand(output, queue: queue);

        await command.RunAsync();

        Assert.That(output.ToString(), Does.Contain("2 test command(s)"));
    }

    [Test]
    public async Task RunAsync_jsonFormat_writesQueueCountAndEntries()
    {
        using var output = new StringWriter();
        var queue = new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], [], "entry-1", ReviewIssueId: "review-42"),
            new CommitEntry("Second", "fix: second", ["b.cs"], [])
        ]);
        var command = BuildCommand(output, queue: queue, operationState: new GitOperationState("merge", true));

        var code = await command.RunAsync(["--format", "json"]);

        using var json = JsonDocument.Parse(output.ToString());
        var entries = json.RootElement.GetProperty("entries");
        var operationState = json.RootElement.GetProperty("operationState");
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(2));
        Assert.That(entries.GetArrayLength(), Is.EqualTo(2));
        Assert.That(entries[0].GetProperty("slice").GetString(), Is.EqualTo("First"));
        Assert.That(entries[0].GetProperty("message").GetString(), Is.EqualTo("fix: first"));
        Assert.That(entries[0].GetProperty("reviewIssueId").GetString(), Is.EqualTo("review-42"));
        Assert.That(entries[1].GetProperty("slice").GetString(), Is.EqualTo("Second"));
        Assert.That(entries[1].GetProperty("message").GetString(), Is.EqualTo("fix: second"));
        Assert.That(operationState.GetProperty("kind").GetString(), Is.EqualTo("merge"));
        Assert.That(operationState.GetProperty("blocking").GetBoolean(), Is.True);
    }

    [Test]
    public async Task RunAsync_jsonFormatWithUnknownArgument_writesStructuredError()
    {
        using var output = new StringWriter();
        var command = BuildCommand(output);

        var code = await command.RunAsync(["--format", "json", "--unknown"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("error").GetString(), Is.EqualTo("Unknown argument: --unknown"));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static CommitsStatusCommand BuildCommand(
        StringWriter output,
        CommitsQueue? queue = null,
        string[]? modifiedFiles = null,
        GitOperationState? operationState = null)
    {
        var service = new CommitsService(
            new FakeCommitsQueueProvider(queue),
            new FakeCommitsGitProvider(modifiedFiles ?? [], operationState));
        return new CommitsStatusCommand(service, new CommitsOutputService(output, new CommitsJsonRenderer()), new CommitsFormatParser());
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(initial ?? CommitsQueue.Empty());

        public Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SavePatchAsync(string slice, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string slice, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider(string[] modifiedFiles, GitOperationState? operationState = null) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(modifiedFiles);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(operationState ?? GitOperationState.None);

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
