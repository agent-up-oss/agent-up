using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Providers;

namespace AgentUp.Server.Tests.Features.TaskSource.Provider;

[TestFixture]
public sealed class TaskSourceStoreTests
{
    [Test]
    public void TheSourceSurvivesARestart()
    {
        var root = Root();
        try
        {
            new JsonTaskSourceStore(root).Write(new TaskSourceSetting("github", "agent-up-oss", "agent-up"));

            Assert.That(new JsonTaskSourceStore(root).Read()!.RepositoryKey, Is.EqualTo("agent-up-oss/agent-up"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AServerThatWasNeverPointedAnywhereReadsAsNothing()
    {
        var root = Root();
        try
        {
            Assert.That(new JsonTaskSourceStore(root).Read(), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ForgettingTheSourceLeavesNothingBehind()
    {
        var root = Root();
        try
        {
            var store = new JsonTaskSourceStore(root);
            store.Write(new TaskSourceSetting("github", "agent-up-oss", "agent-up"));

            store.Write(null);

            Assert.That(store.Read(), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AHalfWrittenFileReadsAsNothingRatherThanStoppingTheServer()
    {
        var root = Root();
        try
        {
            File.WriteAllText(Path.Join(root, "task-source.json"), "{ not json");

            Assert.That(new JsonTaskSourceStore(root).Read(), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TheTokenIsKeptPerHostAndCanBeTakenBack()
    {
        var root = Root();
        try
        {
            var store = new JsonTaskSourceCredentialStore(root);
            store.Write("github", "token-a");
            store.Write("forgejo", "token-b");

            Assert.That(store.Read("github"), Is.EqualTo("token-a"));
            Assert.That(store.Read("forgejo"), Is.EqualTo("token-b"));

            store.Delete("github");

            Assert.That(store.Read("github"), Is.Null);
            Assert.That(store.Read("forgejo"), Is.EqualTo("token-b"), "forgetting one host keeps the other");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AHalfWrittenTokenFileReadsAsNoTokenRatherThanStoppingTheServer()
    {
        var root = Root();
        try
        {
            File.WriteAllText(Path.Join(root, "task-source-credentials.json"), "{ not json");

            Assert.That(new JsonTaskSourceCredentialStore(root).Read("github"), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AStateIsSpentTheFirstTimeItIsUsed()
    {
        var states = new TaskSourceStateStore();
        var state = states.Issue("github");

        Assert.Multiple(() =>
        {
            Assert.That(states.Consume(state), Is.EqualTo("github"));
            Assert.That(states.Consume(state), Is.Null, "a replayed callback finds nothing");
            Assert.That(states.Consume("never-issued"), Is.Null);
        });
    }

    private static string Root()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"task-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
