using System.Text.Json;
using AgentUp.CLI.Features.Commits.Controllers;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Providers;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitsUtilityCommandTests
{
    [Test]
    public async Task Changes_jsonFormat_writesAssignedAndUnassignedFiles()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var command = BuildController(output, new CommitsQueue(2, [entry]), modifiedFiles: ["queued.cs", "loose.cs"]);

        var code = await command.RunAsync(["changes", "--format", "json"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("queuedFiles").GetArrayLength(), Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("unassignedFiles")[0].GetString(), Is.EqualTo("loose.cs"));
    }

    [Test]
    public async Task Changes_whenGitFails_writesStructuredError()
    {
        using var output = new StringWriter();
        var command = BuildController(output, throwOnChanges: true);

        var code = await command.RunAsync(["changes", "--format", "json"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(1));
        Assert.That(json.RootElement.GetProperty("error").GetString(), Does.Contain("git failed"));
    }

    [Test]
    public async Task Help_listsEntryManagementCommands()
    {
        using var output = new StringWriter();
        var command = BuildController(output);

        await command.RunAsync([]);

        var text = output.ToString();
        Assert.That(text, Does.Contain("message"));
        Assert.That(text, Does.Contain("tests"));
        Assert.That(text, Does.Contain("files"));
        Assert.That(text, Does.Contain("remove"));
        Assert.That(text, Does.Contain("restore"));
    }

    [Test]
    public async Task Guard_whenQueueHasEntry_returnsNonZero()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var command = BuildController(output, new CommitsQueue(2, [entry]));

        var code = await command.RunAsync(["guard"]);

        Assert.That(code, Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("still queued"));
    }

    [Test]
    public async Task EditBegin_jsonFormat_returnsSession()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        queue.Patches["entry-1"] = "diff --git a/queued.cs b/queued.cs\n";
        var command = BuildController(output, queueProvider: queue);

        var code = await command.RunAsync(["edit", "begin", "1", "--format", "json"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
        Assert.That(json.RootElement.GetProperty("session").GetProperty("entryId").GetString(), Is.EqualTo("entry-1"));
    }

    [Test]
    public async Task Inspect_whenFormatPrecedesEntry_usesEntryReference()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var command = BuildController(output, new CommitsQueue(2, [entry]));

        var code = await command.RunAsync(["inspect", "--format", "json", "1"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("entry").GetProperty("id").GetString(), Is.EqualTo("entry-1"));
    }

    [Test]
    public async Task EditBegin_whenFormatPrecedesVerb_usesVerbAndEntryReference()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        queue.Patches["entry-1"] = "diff --git a/queued.cs b/queued.cs\n";
        var command = BuildController(output, queueProvider: queue);

        var code = await command.RunAsync(["edit", "--format", "json", "begin", "1"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("session").GetProperty("entryId").GetString(), Is.EqualTo("entry-1"));
    }

    [Test]
    public async Task Message_whenFormatPrecedesEntry_usesEntryReference()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        var command = BuildController(output, queueProvider: queue);

        var code = await command.RunAsync(["message", "--format", "json", "1", "--message", "fix(Slice): updated"]);

        using var json = JsonDocument.Parse(output.ToString());
        Assert.That(code, Is.EqualTo(0));
        Assert.That(json.RootElement.GetProperty("entry").GetProperty("message").GetString(), Is.EqualTo("fix(Slice): updated"));
    }

    [Test]
    public async Task Remove_archivesEntry()
    {
        using var output = new StringWriter();
        var entry = new CommitEntry("Slice", "fix(Slice): msg", ["queued.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        var command = BuildController(output, queueProvider: queue);

        var code = await command.RunAsync(["remove", "1"]);

        Assert.That(code, Is.EqualTo(0));
        Assert.That(queue.Stored!.Commits, Is.Empty);
        Assert.That(queue.Stored.Archived.Single().Entry.Id, Is.EqualTo("entry-1"));
    }

    private static CommitsController BuildController(
        StringWriter output,
        CommitsQueue? queue = null,
        FakeCommitsQueueProvider? queueProvider = null,
        string[]? modifiedFiles = null,
        bool throwOnChanges = false)
    {
        var service = new CommitsService(
            queueProvider ?? new FakeCommitsQueueProvider(queue),
            new FakeCommitsGitProvider(modifiedFiles ?? [], throwOnChanges));
        var formatParser = new CommitsFormatParser();
        var outputService = new CommitsOutputService(output, new CommitsJsonRenderer());
        var enqueueParser = new CommitsArgParser();
        var utilityRunner = new CommitsUtilityCommandRunner(service, outputService, formatParser);
        return new CommitsController(
            new CommitsEnqueueCommand(service, enqueueParser, outputService),
            new CommitsStatusCommand(service, outputService, formatParser),
            new CommitsChangesCommand(utilityRunner),
            new CommitsInspectCommand(utilityRunner),
            new CommitsEditCommand(utilityRunner),
            new CommitsEntryCommand(utilityRunner),
            new CommitsGuardCommand(service, outputService, formatParser),
            new CommitsNextCommand(service, outputService, formatParser),
            new CommitsClearCommand(service, outputService),
            outputService);
    }

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;
        public Dictionary<string, string> Patches { get; } = [];

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

        public Task SavePatchAsync(string patchKey, string patch, CancellationToken cancellationToken = default)
        {
            Patches[patchKey] = patch;
            return Task.CompletedTask;
        }

        public Task<string?> ReadPatchAsync(string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Patches.GetValueOrDefault(patchKey));

        public Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider(string[] modifiedFiles, bool throwOnChanges = false) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnChanges)
                throw new InvalidOperationException("git failed");

            return Task.FromResult<IReadOnlyList<string>>(modifiedFiles);
        }

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult("diff --git a/queued.cs b/queued.cs\n");

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
