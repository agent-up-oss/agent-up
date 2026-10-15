using AgentUp.Server.Features.Forge.Providers;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeOAuthStateStoreTests
{
    [Test]
    public void Consume_returnsTheWorkspaceOnce()
    {
        var store = new ForgeOAuthStateStore();
        var state = store.Issue("Primary");

        Assert.That(store.Consume(state), Is.EqualTo("Primary"));
        Assert.That(store.Consume(state), Is.Null);
    }

    [Test]
    public void Consume_returnsNullForAnUnknownState()
    {
        Assert.That(new ForgeOAuthStateStore().Consume("missing"), Is.Null);
    }
}
