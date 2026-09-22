using AgentUp.Desktop.Features.Authentication.Interfaces;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.FakeServer.Controllers;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Features.FakeServer.Services;

namespace AgentUp.Desktop.Tests.Support;

internal static class FakeServerTestComposition
{
    public static FakeBackendService Backend()
        => new(new FakeServerDefinitionProvider().LoadEmbedded());

    public static FakeServerController Controller(FakeBackendService? backend = null)
        => new(backend ?? Backend());

    public static ServerConnectionService Connections(
        IServerConnectionStore store,
        HttpClient http,
        FakeBackendService? backend = null)
        => new(store, http, Controller(backend));

    public static HttpClient Client(FakeBackendService backend)
        => ServerSessionProvider.CreateClient(
            new Uri("http://127.0.0.1:5000"),
            new FakeServerMessageHandler(backend, new HttpClientHandler()));
}
