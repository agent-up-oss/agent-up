using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentCliHomeProviderTests
{
    [Test]
    public void Constructor_keepsTheHomeUnderTheDataDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-cli-home-root");
        try
        {
            var home = new AgentCliHomeProvider(root.FullName);
            home.Ensure();

            Assert.Multiple(() =>
            {
                Assert.That(home.HomePath, Does.StartWith(root.FullName));
                Assert.That(Directory.Exists(home.HomePath), Is.True);
                Assert.That(AgentCliHomeProvider.IsUnderRoot(root.FullName, home.HomePath), Is.True);
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void ClaudeCredentialStore_writesOnlySubscriptionOauthTokens()
    {
        var root = Directory.CreateTempSubdirectory("agent-cli-token-root");
        try
        {
            var store = new AgentClaudeCredentialStore(new AgentCliHomeProvider(root.FullName));
            store.Write("sk-ant-oat01-secret");

            Assert.That(store.Read(), Is.EqualTo("sk-ant-oat01-secret"));
            Assert.Throws<InvalidOperationException>(() => store.Write("sk-ant-api03-usage-key"));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void ClaudeCredentialStore_returnsNullWhenTheTokenFileIsMissingOrUnreadable()
    {
        var root = Directory.CreateTempSubdirectory("agent-cli-token-missing");
        try
        {
            var home = new AgentCliHomeProvider(root.FullName);
            var store = new AgentClaudeCredentialStore(home);
            var tokenPath = Path.Join(home.HomePath, ".claude-oauth-token");
            File.WriteAllText(tokenPath, "not-a-subscription-token");

            Assert.That(store.Read(), Is.Null);

            File.Delete(tokenPath);
            Assert.That(store.Read(), Is.Null);

            Directory.CreateDirectory(tokenPath);
            Assert.That(store.Read(), Is.Null);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void IsUnderRoot_rejectsPathsOutsideTheDataDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-cli-home-root");
        try
        {
            Assert.That(AgentCliHomeProvider.IsUnderRoot(root.FullName, Path.Join(root.Parent!.FullName, "other")), Is.False);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void ProcessEnvironment_setsHomeAndInjectsAStoredClaudeToken()
    {
        var root = Directory.CreateTempSubdirectory("agent-cli-env-root");
        try
        {
            var home = new AgentCliHomeProvider(root.FullName);
            var store = new AgentClaudeCredentialStore(home);
            store.Write("sk-ant-oat01-secret");
            var environment = new AgentProcessEnvironmentProvider(home, store);

            var claude = environment.EnvironmentFor("claude");
            var cursor = environment.EnvironmentFor("cursor");

            Assert.Multiple(() =>
            {
                Assert.That(claude["HOME"], Is.EqualTo(home.HomePath));
                Assert.That(claude["CLAUDE_CODE_OAUTH_TOKEN"], Is.EqualTo("sk-ant-oat01-secret"));
                Assert.That(cursor.ContainsKey("CLAUDE_CODE_OAUTH_TOKEN"), Is.False);
                Assert.That(cursor["HOME"], Is.EqualTo(home.HomePath),
                    "Every agent CLI keeps its credentials under the Server data directory");
            });
        }
        finally
        {
            root.Delete(true);
        }
    }
}
