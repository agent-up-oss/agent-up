using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

/// <summary>
/// The Demo's own task source. It starts pointed at the example repository and signed in, so the
/// Tasks surface has something to offer without a round trip to a host that is not there.
/// </summary>
[TestFixture]
public sealed class FakeBackendTaskSourceTests
{
    [Test]
    public void TheDemoStartsPointedAtTheExampleRepositoryAndSignedIn()
    {
        var backend = FakeServerTestComposition.Backend();

        var source = Json(backend, Get("/api/task-source"));

        Assert.Multiple(() =>
        {
            Assert.That(source["configured"]!.GetValue<bool>(), Is.True);
            Assert.That(source["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(source["owner"]!.GetValue<string>(), Is.EqualTo("example"));
            Assert.That(source["repository"]!.GetValue<string>(), Is.EqualTo("repository"));
        });
    }

    [Test]
    public void TheDemoOffersTheExampleRepositorysOpenIssues()
    {
        var backend = FakeServerTestComposition.Backend();

        var issues = Json(backend, Get("/api/task-source/issues")).AsArray();

        Assert.That(issues, Is.Not.Empty);
    }

    [Test]
    public void NamingAnotherRepositoryKeepsIt()
    {
        var backend = FakeServerTestComposition.Backend();

        var source = Json(backend, Put("/api/task-source", """{"moduleId":"github","owner":"acme","repository":"widgets"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(source["owner"]!.GetValue<string>(), Is.EqualTo("acme"));
            Assert.That(source["repository"]!.GetValue<string>(), Is.EqualTo("widgets"));
        });
    }

    [Test]
    public void ForgettingTheSourceLeavesNothingConfiguredAndNothingToOffer()
    {
        var backend = FakeServerTestComposition.Backend();

        var source = Json(backend, Delete("/api/task-source"));
        var issues = Json(backend, Get("/api/task-source/issues")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(source["configured"]!.GetValue<bool>(), Is.False);
            Assert.That(issues, Is.Empty);
        });
    }

    [Test]
    public void SigningInBeforeARepositoryIsNamedIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();
        Run(backend, Delete("/api/task-source"));

        Assert.That(Run(backend, Post("/api/task-source/login")).Status, Is.EqualTo(409));
    }

    [Test]
    public void SigningInConnectsTheSourceAndHandsBackSomewhereToGo()
    {
        var backend = FakeServerTestComposition.Backend();
        Run(backend, Delete("/api/task-source"));
        Run(backend, Put("/api/task-source", """{"moduleId":"github","owner":"example","repository":"repository"}"""));

        var login = Json(backend, Post("/api/task-source/login"));

        Assert.Multiple(() =>
        {
            Assert.That(login["authorizeUrl"]!.GetValue<string>(), Does.Contain("authorize"));
            Assert.That(Json(backend, Get("/api/task-source"))["connected"]!.GetValue<bool>(), Is.True);
        });
    }

    [Test]
    public void TaskSource_saysNotFoundForARouteTheSurfaceNeverAsksFor()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Get("/api/task-source/nonsense")).Status, Is.EqualTo(404));
    }

    private static FakeBackendRequestDtoBuilder Get(string path) => new FakeBackendRequestDtoBuilder().Get(path);

    private static FakeBackendRequestDtoBuilder Put(string path, string body)
        => new FakeBackendRequestDtoBuilder().Put(path, body);

    private static FakeBackendRequestDtoBuilder Post(string path)
        => new FakeBackendRequestDtoBuilder().Post(path);

    private static FakeBackendRequestDtoBuilder Delete(string path)
        => new FakeBackendRequestDtoBuilder().Delete(path);

    private static FakeBackendResponseDto Run(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => backend.Handle(request.Build());

    private static JsonNode Json(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => JsonNode.Parse(Run(backend, request).Body!)!;
}
