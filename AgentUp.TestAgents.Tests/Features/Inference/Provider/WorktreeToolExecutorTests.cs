using AgentUp.TestAgents.Features.Inference.Models;
using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class WorktreeToolExecutorTests
{
    [Test]
    public void Execute_writesAndReadsAFileUnderTheWorktree()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            var tools = new WorktreeToolExecutor();
            var written = tools.Execute(worktree, Call("write_file", "path", "hello.txt", "content", "hi"));
            var read = tools.Execute(worktree, Call("read_file", "path", "hello.txt"));

            Assert.Multiple(() =>
            {
                Assert.That(written.Succeeded, Is.True);
                Assert.That(read.Output, Is.EqualTo("hi"));
            });
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_rejectsAPathThatEscapesTheWorktree()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            var result = new WorktreeToolExecutor().Execute(
                worktree, Call("read_file", "path", "../outside.txt"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Output, Does.Contain("outside the worktree"));
            });
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_completeTaskRequiresASummary()
    {
        var result = new WorktreeToolExecutor().Execute("/tmp", Call("complete_task"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.CompletesTask, Is.False);
    }

    [Test]
    public void Execute_rejectsAnUnknownToolName()
    {
        var result = new WorktreeToolExecutor().Execute("/tmp", Call("explode"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Output, Does.Contain("Unknown tool"));
        });
    }

    [Test]
    public void Execute_listsFilesUnderTheWorktree()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            File.WriteAllText(Path.Join(worktree, "alpha.txt"), "a");
            File.WriteAllText(Path.Join(worktree, "beta.txt"), "b");

            var result = new WorktreeToolExecutor().Execute(worktree, Call("list_files", "path", "."));

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Output, Does.Contain("alpha.txt"));
                Assert.That(result.Output, Does.Contain("beta.txt"));
            });
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_listsTheWorktreeRootWhenNoPathIsGiven()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            File.WriteAllText(Path.Join(worktree, "root.txt"), "r");

            var result = new WorktreeToolExecutor().Execute(worktree, Call("list_files"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Output, Does.Contain("root.txt"));
            });
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_rejectsAListPathThatEscapesTheWorktree()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            var result = new WorktreeToolExecutor().Execute(
                worktree, Call("list_files", "path", "../outside"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Output, Does.Contain("outside the worktree"));
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_failsWhenTheListedDirectoryIsMissing()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            var result = new WorktreeToolExecutor().Execute(worktree, Call("list_files", "path", "nope"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Output, Does.Contain("does not exist"));
            });
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_requiresAPathForFileTools()
    {
        var result = new WorktreeToolExecutor().Execute("/tmp", Call("read_file"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Output, Does.Contain("A path is required"));
        });
    }

    [Test]
    public void Execute_returnsTheReadErrorWhenThePathIsADirectory()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            var result = new WorktreeToolExecutor().Execute(worktree, Call("read_file", "path", "."));

            Assert.That(result.Succeeded, Is.False);
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_returnsTheWriteErrorWhenThePathIsADirectory()
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-infer-tools").FullName;
        try
        {
            Directory.CreateDirectory(Path.Join(worktree, "blocked"));
            var result = new WorktreeToolExecutor().Execute(
                worktree, Call("write_file", "path", "blocked", "content", "nope"));

            Assert.That(result.Succeeded, Is.False);
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public void Execute_requiresAPathBeforeWriting()
    {
        var result = new WorktreeToolExecutor().Execute("/tmp", Call("write_file", "content", "hi"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Output, Does.Contain("A path is required"));
    }

    private static ParsedToolCall Call(string name, params string[] pairs)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < pairs.Length; index += 2)
            arguments[pairs[index]] = pairs[index + 1];
        return new ParsedToolCall(name, arguments);
    }
}
