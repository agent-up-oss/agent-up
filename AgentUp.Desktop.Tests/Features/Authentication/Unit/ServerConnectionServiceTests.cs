using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class ServerConnectionServiceTests
{
    [Test]
    public void Save_normalizesUrlStoresTokenAndAppliesSession()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        var saved = service.Save("http://127.0.0.1:5100/", "token-1");

        Assert.Multiple(() =>
        {
            Assert.That(saved.Url, Is.EqualTo("http://127.0.0.1:5100"));
            Assert.That(saved.HasCredential, Is.True);
            Assert.That(saved.IsActive, Is.True);
            Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
            Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-1"));
            Assert.That(UserServers(service), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Save_updatesExistingServerInsteadOfDuplicating()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        var first = service.Save("http://127.0.0.1:5100", "token-1");

        var second = service.Save("http://127.0.0.1:5100/", "token-2");

        Assert.Multiple(() =>
        {
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(UserServers(service), Has.Count.EqualTo(1));
            Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-2"));
        });
    }

    [Test]
    public void Activate_appliesSavedCredentialAndRemoveDropsIt()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        var local = service.Save("http://127.0.0.1:5000", "local-token");
        var remote = service.Save("https://agent-up.example.com", "remote-token");

        var activated = service.Activate(local.Id);
        Assert.That(activated.Url, Is.EqualTo("http://127.0.0.1:5000"));
        Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("local-token"));

        service.Remove(local.Id);
        var remaining = UserServers(service);
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Id, Is.EqualTo(remote.Id));
        Assert.That(remaining[0].IsActive, Is.True);
    }

    [Test]
    public void RestoreActive_appliesTheStoredSelection()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        service.Save("https://agent-up.example.com", "remote-token");

        using var restored = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var next = FakeServerTestComposition.Connections(store, restored);
        next.RestoreActive();

        Assert.That(restored.BaseAddress, Is.EqualTo(new Uri("https://agent-up.example.com/")));
        Assert.That(restored.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("remote-token"));
    }

    [Test]
    public void Save_withoutTokenKeepsPreviousCredential()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        service.Save("http://127.0.0.1:5000", "token-1");

        var saved = service.Save("http://127.0.0.1:5000", null);

        Assert.That(saved.HasCredential, Is.True);
        Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-1"));
    }

    [Test]
    public void Activate_throwsWhenTheSavedServerIsGone()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        Assert.That(
            () => service.Activate("missing"),
            Throws.InvalidOperationException.With.Message.EqualTo("That saved server is no longer available."));
    }

    [Test]
    public void RestoreActive_doesNothingWhenNoServersAreSaved()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        service.RestoreActive();

        Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5000")));
        Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
    }

    [Test]
    public void RestoreActive_usesTheFirstServerWhenTheActiveIdIsMissing()
    {
        var store = new InMemoryServerConnectionStore();
        store.Save(new ServerSelection
        {
            Servers =
            [
                new ConfiguredServer
                {
                    Id = "one",
                    Url = "http://127.0.0.1:5100",
                    AccessToken = "token-1"
                }
            ]
        });
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        service.RestoreActive();

        Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
        Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-1"));
    }

    [Test]
    public void Prepare_appliesASavedTokenForTheEnteredUrl()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        service.Save("http://127.0.0.1:5100", "saved-token");

        service.Prepare("http://127.0.0.1:5100/");

        Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
        Assert.That(http.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("saved-token"));
    }

    [Test]
    public void Remove_clearsTheLastServer()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        var saved = service.Save("http://127.0.0.1:5100", "token-1");

        service.Remove(saved.Id);

        var remaining = service.List();
        Assert.Multiple(() =>
        {
            Assert.That(remaining.Servers.All(server => server.IsFake), Is.True);
            Assert.That(remaining.CurrentUrl, Is.EqualTo(""));
        });
    }

    [Test]
    public void CurrentUrl_fallsBackToTheHttpClientBaseAddress()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5100/") };
        var service = FakeServerTestComposition.Connections(store, http);

        Assert.That(service.CurrentUrl(), Is.EqualTo("http://127.0.0.1:5100"));
    }

    [Test]
    public void List_alwaysIncludesTheBuiltInFakeServer()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        var listed = service.List().Servers;

        Assert.Multiple(() =>
        {
            Assert.That(listed[0].IsFake, Is.True);
            Assert.That(listed[0].CanRemove, Is.False);
            Assert.That(listed[0].DisplayName, Is.EqualTo("Demo"));
            Assert.That(listed[0].Url, Is.EqualTo("http://127.0.0.1:9"));
        });
    }

    [Test]
    public void Save_fakeServerAppliesTheInProcessBackendWithoutAPassword()
    {
        var store = new InMemoryServerConnectionStore();
        var backend = FakeServerTestComposition.Backend();
        using var http = FakeServerTestComposition.Client(backend);
        var service = FakeServerTestComposition.Connections(store, http, backend);

        var saved = service.Save("http://127.0.0.1:9", null);

        Assert.Multiple(() =>
        {
            Assert.That(saved.IsFake, Is.True);
            Assert.That(saved.IsActive, Is.True);
            Assert.That(service.CurrentUrl(), Is.EqualTo("http://127.0.0.1:9"));
        });
    }

    [Test]
    public void Remove_refusesTheBuiltInFakeServer()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        service.Remove("fake");

        Assert.That(service.List().Servers[0].IsFake, Is.True);
    }

    private static List<AgentUp.Desktop.Features.Authentication.DTOs.SavedServerDto> UserServers(
        ServerConnectionService service)
        => service.List().Servers.Where(server => !server.IsFake).ToList();
}
