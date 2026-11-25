using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Providers;

namespace AgentUp.Server.Tests.Features.TaskSource.Provider;

[TestFixture]
public sealed class TaskSourceStoreTests
{
    [Test]
    public void TheTokenAndHostAreKeptPerMemberAndCanBeTakenBack()
    {
        var root = Root();
        try
        {
            var store = new JsonTaskSourceCredentialStore(root);
            store.Write("bob", new TaskSourceCredential("token-a", "https://github.com", "github"));
            store.Write("frank", new TaskSourceCredential("token-b", "https://git.example.test", "forgejo"));

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("bob")?.Token, Is.EqualTo("token-a"));
                Assert.That(store.Read("bob")?.EndpointUrl, Is.EqualTo("https://github.com"));
                Assert.That(store.Read("bob")?.ModuleId, Is.EqualTo("github"));
                Assert.That(store.Read("frank")?.Token, Is.EqualTo("token-b"));
                Assert.That(store.Keys(), Is.EquivalentTo(new[] { "bob", "frank" }));
            });

            store.Delete("bob");

            Assert.Multiple(() =>
            {
                Assert.That(store.Read("bob"), Is.Null);
                Assert.That(store.Read("frank")?.Token, Is.EqualTo("token-b"), "forgetting one member keeps the other");
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
            Assert.That(stored?.ModuleId, Is.EqualTo("github"));
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
                Assert.That(store.Read("github")?.ModuleId, Is.EqualTo("github"));
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
                Assert.That(stored?.ModuleId, Is.EqualTo("github"));
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
                Assert.That(store.Read("forgejo")?.ModuleId, Is.EqualTo("forgejo"));
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
