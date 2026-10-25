using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class GithubAppConnectionStoreTests
{
    [Test]
    public void Write_keepsOneConnectionPerWorkspaceAndFindsItsCallbacks()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-github-app-store-");
        var store = new GithubAppConnectionStore(directory.FullName);
        var first = Connection("workspace-1", "state-1", "setup-1") with { InstallationId = 12 };

        store.Write(first);
        store.Write(first with { InstallationId = 34 });

        Assert.Multiple(() =>
        {
            Assert.That(store.Read("workspace-1")!.InstallationId, Is.EqualTo(34));
            Assert.That(store.FindByState("state-1")!.WorkspaceId, Is.EqualTo("workspace-1"));
            Assert.That(store.FindBySetupKey("setup-1")!.WorkspaceId, Is.EqualTo("workspace-1"));
            Assert.That(store.FindByState("short"), Is.Null);
        });
    }

    private static GithubAppConnection Connection(string workspaceId, string state, string setupKey)
        => new(
            workspaceId,
            state,
            setupKey,
            "Agent-Up-Workspace-12345678",
            "http://localhost:5000",
            "https://api.github.com/",
            "https://github.com",
            1,
            "agent-up-workspace-12345678",
            "private-key");
}
