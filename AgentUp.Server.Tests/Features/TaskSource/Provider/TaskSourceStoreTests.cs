using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Providers;

namespace AgentUp.Server.Tests.Features.TaskSource.Provider;

[TestFixture]
public sealed class TaskSourceStoreTests
{
    [Test]
    public void TheTokenAndHostAreKeptPerModuleAndCanBeTakenBack()
    {
        var root = Root();
        try
        {
            var store = new JsonTaskSourceCredentialStore(root);
            store.Write("github", new TaskSourceCredential("token-a", "https://github.com"));
            store.Write("forgejo", new TaskSourceCredential("token-b", "https://git.example.test"));

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("github")?.Token, Is.EqualTo("token-a"));
                Assert.That(store.Read("github")?.EndpointUrl, Is.EqualTo("https://github.com"));
                Assert.That(store.Read("forgejo")?.Token, Is.EqualTo("token-b"));
            });

            store.Delete("github");

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("github"), Is.Null);
                Assert.That(store.Read("forgejo")?.Token, Is.EqualTo("token-b"), "forgetting one host keeps the other");
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ALegacyTokenOnlyFileReadsTheTokenAndHasNoHostUntilItIsSavedAgain()
    {
        var root = Root();
        try
        {
            File.WriteAllText(Path.Join(root, "task-source-credentials.json"), """{"github":"legacy-token"}""");

            var stored = new JsonTaskSourceCredentialStore(root).Read("github");

            Assert.Multiple(() =>
            {
                Assert.That(stored?.Token, Is.EqualTo("legacy-token"));
                Assert.That(stored?.EndpointUrl, Is.EqualTo(""));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AnObjectWithoutATokenIsSkippedRatherThanStoredAsEmpty()
    {
        var root = Root();
        try
        {
            File.WriteAllText(
                Path.Join(root, "task-source-credentials.json"),
                """{"github":{"token":"kept","endpointUrl":"https://github.com"},"empty":{}}""");

            var store = new JsonTaskSourceCredentialStore(root);

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("github")?.Token, Is.EqualTo("kept"));
                Assert.That(store.Read("empty"), Is.Null);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AnObjectWithoutAHostUrlKeepsTheTokenAndAnEmptyEndpoint()
    {
        var root = Root();
        try
        {
            File.WriteAllText(
                Path.Join(root, "task-source-credentials.json"),
                """{"github":{"token":"kept"}}""");

            var stored = new JsonTaskSourceCredentialStore(root).Read("github");

            Assert.Multiple(() =>
            {
                Assert.That(stored?.Token, Is.EqualTo("kept"));
                Assert.That(stored?.EndpointUrl, Is.EqualTo(""));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AMissingFileReadsAsNoToken()
    {
        var root = Root();
        try
        {
            Assert.That(new JsonTaskSourceCredentialStore(root).Read("github"), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ANonObjectValueIsSkippedRatherThanStoredAsEmpty()
    {
        var root = Root();
        try
        {
            File.WriteAllText(
                Path.Join(root, "task-source-credentials.json"),
                """{"github":["not","an","object"],"empty":"","forgejo":{"token":"kept","endpointUrl":"https://git.example.test"}}""");

            var store = new JsonTaskSourceCredentialStore(root);

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("github"), Is.Null);
                Assert.That(store.Read("empty"), Is.Null);
                Assert.That(store.Read("forgejo")?.Token, Is.EqualTo("kept"));
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void DeletingAHostThatWasNeverSavedLeavesTheFileAlone()
    {
        var root = Root();
        try
        {
            var store = new JsonTaskSourceCredentialStore(root);
            store.Delete("github");

            Assert.That(File.Exists(Path.Join(root, "task-source-credentials.json")), Is.False);
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
