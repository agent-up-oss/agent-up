using AgentUp.Desktop.Features.Authentication.DTOs;
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
    public void ANewSession_doesNotApplyAStoredSelectionUntilTheUserChooses()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        service.Save("https://agent-up.example.com", "remote-token");

        using var launchHttp = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var next = FakeServerTestComposition.Connections(store, launchHttp);

        Assert.Multiple(() =>
        {
            Assert.That(launchHttp.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5000")));
            Assert.That(launchHttp.DefaultRequestHeaders.Authorization, Is.Null);
            Assert.That(
                UserServers(next).Any(server => server.Url == "https://agent-up.example.com"),
                Is.True);
        });
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
    public void ANewSession_doesNotApplyAStoredDemoSelection()
    {
        var store = new InMemoryServerConnectionStore();
        using var firstHttp = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        FakeServerTestComposition.Connections(store, firstHttp).Activate("fake");

        using var launchHttp = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var next = FakeServerTestComposition.Connections(store, launchHttp);

        Assert.Multiple(() =>
        {
            Assert.That(next.CurrentUrl(), Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(next.List().Servers[0].IsActive, Is.True);
        });
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

    [Test]
    public void List_includesTheRecommendedServerAfterDemo()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("recommended", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        var servers = service.List().Servers;

        Assert.Multiple(() =>
        {
            Assert.That(servers[0].IsFake, Is.True);
            Assert.That(servers[1].IsRecommended, Is.True);
            Assert.That(servers[1].Url, Is.EqualTo("http://127.0.0.1:5288"));
            Assert.That(servers[1].CanRemove, Is.False);
        });
    }

    [Test]
    public void Remove_refusesTheRecommendedServer()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("recommended", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        service.Remove("recommended");

        Assert.That(service.List().Servers.Any(server => server.IsRecommended), Is.True);
    }

    [Test]
    public void Activate_fakeIdSwitchesOntoDemo()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        var activated = service.Activate("fake");

        Assert.Multiple(() =>
        {
            Assert.That(activated.IsFake, Is.True);
            Assert.That(activated.IsActive, Is.True);
            Assert.That(service.CurrentUrl(), Is.EqualTo("http://127.0.0.1:9"));
        });
    }

    [Test]
    public void Prepare_fakeUrlResetsTheBackend()
    {
        var store = new InMemoryServerConnectionStore();
        var backend = FakeServerTestComposition.Backend();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http, backend);
        backend.Handle(new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop").Build());

        service.Prepare("http://127.0.0.1:9");

        Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        Assert.That(service.CurrentUrl(), Is.EqualTo("http://127.0.0.1:9"));
    }

    [Test]
    public void Activate_appliesTheAgentEventsClientSession()
    {
        var store = new InMemoryServerConnectionStore();
        var backend = FakeServerTestComposition.Backend();
        using var http = FakeServerTestComposition.Client(backend);
        using var eventsHttp = FakeServerTestComposition.Client(backend);
        var service = new ServerConnectionService(
            store,
            http,
            FakeServerTestComposition.Controller(backend),
            eventsHttp);

        service.Activate("fake");

        Assert.That(ServerSessionProvider.CurrentUri(eventsHttp)!.GetLeftPart(UriPartial.Authority), Is.EqualTo("http://127.0.0.1:9"));
        Assert.That(eventsHttp.DefaultRequestHeaders.Authorization, Is.Null);
    }

    [Test]
    public void Activate_treatsAStoredFakeUrlAsDemo()
    {
        var store = new InMemoryServerConnectionStore();
        store.Save(new ServerSelection
        {
            Servers =
            [
                new ConfiguredServer
                {
                    Id = "legacy-demo",
                    Url = "http://127.0.0.1:9"
                }
            ],
            ActiveServerId = "legacy-demo"
        });
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        var activated = service.Activate("legacy-demo");

        Assert.That(activated.IsFake, Is.True);
        Assert.That(service.CurrentUrl(), Is.EqualTo("http://127.0.0.1:9"));
    }

    [Test]
    public void Surfaces_hidesDemoUnsupportedChrome()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);

        service.Activate("fake");

        Assert.That(service.Surfaces(), Is.EqualTo(ClientSurfaceAvailability.Demo));
    }

    [Test]
    public void Surfaces_exposesRealServerChrome()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        service.Save("http://127.0.0.1:5100", "token-1");

        Assert.That(service.Surfaces(), Is.EqualTo(ClientSurfaceAvailability.Real));
    }

    [Test]
    public void CurrentId_returnsTheActiveSavedServerId()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var service = FakeServerTestComposition.Connections(store, http);
        var saved = service.Save("http://127.0.0.1:5100", "token-1");

        Assert.That(service.CurrentId(), Is.EqualTo(saved.Id));
    }

    [Test]
    public void CurrentId_fallsBackToTheUrlWhenNoSavedServerMatches()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5100/") };
        var service = FakeServerTestComposition.Connections(store, http);

        Assert.That(service.CurrentId(), Is.EqualTo("http://127.0.0.1:5100"));
    }

    [Test]
    public void Save_usesTheRecommendedIdForTheRecommendedUrl()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("recommended", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        var saved = service.Save("http://127.0.0.1:5288", "token-1");

        Assert.Multiple(() =>
        {
            Assert.That(saved.Id, Is.EqualTo("recommended"));
            Assert.That(saved.IsRecommended, Is.True);
            Assert.That(saved.CanRemove, Is.False);
            Assert.That(saved.DisplayName, Is.EqualTo("Agent-Up Cloud"));
        });
    }

    [Test]
    public void Activate_connectsTheRecommendedServerByConfiguredId()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("cloud", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        var activated = service.Activate("cloud");

        Assert.Multiple(() =>
        {
            Assert.That(activated.Url, Is.EqualTo("http://127.0.0.1:5288"));
            Assert.That(activated.IsRecommended, Is.True);
            Assert.That(service.CurrentId(), Is.EqualTo("cloud"));
        });
    }

    [Test]
    public void Activate_connectsTheRecommendedServerByAlias()
    {
        var store = new InMemoryServerConnectionStore();
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("cloud", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        var activated = service.Activate("recommended");

        Assert.That(activated.Url, Is.EqualTo("http://127.0.0.1:5288"));
        Assert.That(activated.IsRecommended, Is.True);
    }

    [Test]
    public void Remove_refusesASavedCopyOfTheRecommendedUrl()
    {
        var store = new InMemoryServerConnectionStore();
        store.Save(new ServerSelection
        {
            Servers =
            [
                new ConfiguredServer
                {
                    Id = "copied",
                    Url = "http://127.0.0.1:5288"
                }
            ],
            ActiveServerId = "copied"
        });
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5000") };
        var recommended = new RecommendedServer("recommended", "http://127.0.0.1:5288", "Agent-Up Cloud");
        var service = FakeServerTestComposition.Connections(store, http, recommended: recommended);

        service.Remove("copied");

        Assert.That(service.List().Servers.Any(server => server.IsRecommended), Is.True);
        Assert.That(store.Load().Servers.Any(server => server.Id == "copied"), Is.True);
    }

    private static List<SavedServerDto> UserServers(
        ServerConnectionService service)
        => service.List().Servers.Where(server => !server.IsFake).ToList();
}
