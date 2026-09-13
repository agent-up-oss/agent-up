using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class InMemoryServerConnectionStoreTests
{
    [Test]
    public void Save_clonesSelectionSoLaterMutationsDoNotLeak()
    {
        var store = new InMemoryServerConnectionStore();
        var selection = new ServerSelection
        {
            ActiveServerId = "one",
            Servers = [new ConfiguredServer { Id = "one", Url = "http://localhost:5000", AccessToken = "token" }]
        };

        store.Save(selection);
        selection.Servers[0].AccessToken = "mutated";

        Assert.That(store.Load().Servers[0].AccessToken, Is.EqualTo("token"));
    }

    [Test]
    public void Load_returnsIndependentCopies()
    {
        var store = new InMemoryServerConnectionStore();
        store.Save(new ServerSelection
        {
            ActiveServerId = "one",
            Servers = [new ConfiguredServer { Id = "one", Url = "http://localhost:5000" }]
        });

        var loaded = store.Load();
        loaded.Servers[0].Url = "https://other.example";

        Assert.That(store.Load().Servers[0].Url, Is.EqualTo("http://localhost:5000"));
    }
}
