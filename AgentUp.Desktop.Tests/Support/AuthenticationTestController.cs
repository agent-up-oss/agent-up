using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;

namespace AgentUp.Desktop.Tests.Support;

internal static class AuthenticationTestController
{
    public static AuthenticationController Create(
        DisposableTestHttpClient http,
        InMemoryServerConnectionStore? store = null,
        RecommendedServer? recommended = null)
        => new(
            new AuthenticationService(new AuthenticationApiClient(http.Client)),
            new ServerConnectionService(
                store ?? new InMemoryServerConnectionStore(),
                http.Client,
                FakeServerTestComposition.Controller(),
                recommended: recommended));
}
