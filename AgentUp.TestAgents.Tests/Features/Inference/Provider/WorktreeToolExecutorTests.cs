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

    private static ParsedToolCall Call(string name, params string[] pairs)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < pairs.Length; index += 2)
            arguments[pairs[index]] = pairs[index + 1];
        return new ParsedToolCall(name, arguments);
    }
}
