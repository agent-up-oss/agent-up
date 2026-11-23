using AgentUp.Server.Features.TaskSource.Providers;

namespace AgentUp.Server.Tests.Features.TaskSource.Provider;

[TestFixture]
public sealed class TaskSourceStoreTests
{
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

    private static string Root()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, $"task-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
