using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

[TestFixture]
public sealed class FakeBackendServiceTests
{
    [Test]
    public void Handle_authenticationAndCatalogDoNotRequireAPassword()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.Multiple(() =>
        {
            Assert.That(Json(backend, Get("/api/auth/status"))["authenticationRequired"]!.GetValue<bool>(), Is.False);
            Assert.That(Json(backend, Post("/api/auth/login"))["accessToken"]!.GetValue<string>(), Is.EqualTo("fake-token"));
            Assert.That(Json(backend, Get("/api/connection"))["kind"]!.GetValue<string>(), Is.EqualTo("selfHosted"));
            Assert.That(Json(backend, Get("/api/entitlements"))["edition"]!.GetValue<string>(), Is.EqualTo("community"));
        });
    }

    [Test]
    public void Handle_listsTheBundledWorkspaceAndOverview()
    {
        var backend = FakeServerTestComposition.Backend();
        var workspaces = Json(backend, Get("/api/workspaces")).AsArray();
        var overview = Json(backend, Get("/api/workspaces/harbor-shop/overview"));

        Assert.Multiple(() =>
        {
            Assert.That(workspaces[0]!["id"]!.GetValue<string>(), Is.EqualTo("harbor-shop"));
            Assert.That(workspaces[0]!["state"]!.GetValue<string>(), Is.EqualTo("Running"));
            Assert.That(overview["displayName"]!.GetValue<string>(), Is.EqualTo("Harbor Shop"));
            Assert.That(overview["applicationCount"]!.GetValue<int>(), Is.EqualTo(2));
        });
    }

    [Test]
    public void Handle_startAndStopChangeWorkspaceState()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Running"));
    }

    [Test]
    public void Handle_gitAndConsoleReturnTheBundledDemo()
    {
        var backend = FakeServerTestComposition.Backend();
        var changes = Json(backend, Get("/api/workspaces/harbor-shop/git/changes"));
        var diff = Json(backend, Get("/api/workspaces/harbor-shop/git/file").WithQuery("?path=apps/storefront/ProductGrid.tsx"));
        var lines = Json(backend, Get("/api/workspaces/harbor-shop/applications/Storefront/output")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(diff["path"]!.GetValue<string>(), Is.EqualTo("apps/storefront/ProductGrid.tsx"));
            Assert.That(lines[1]!.GetValue<string>(), Does.Contain("Storefront listening"));
        });
    }

    [Test]
    public void Handle_agentPromptPublishesScriptedEvents()
    {
        var backend = FakeServerTestComposition.Backend();
        var published = new List<string>();
        using var subscription = backend.SubscribeAgent("harbor-shop", item => published.Add(item.Type));

        var result = Run(backend, Post("/api/workspaces/harbor-shop/agent/messages", """{"message":"status?"}"""));
        var replayed = backend.AgentEventsAfter("harbor-shop", 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(204));
            Assert.That(published, Does.Contain("user_message"));
            Assert.That(published, Does.Contain("session_update"));
            Assert.That(replayed.Select(item => item.Type), Does.Contain("user_message"));
            Assert.That(Json(backend, Get("/api/workspaces/harbor-shop/agent"))["state"]!.GetValue<string>(), Is.EqualTo("ready"));
        });
    }

    [Test]
    public void Handle_issuesTicketsAndServesBundledHtml()
    {
        var backend = FakeServerTestComposition.Backend();
        var ticket = Json(backend, Post("/api/apps/tickets", """{"workspaceId":"harbor-shop","allocatedPort":9100}"""));
        var html = Run(backend, Get("/apps/harbor-shop/storefront"));

        Assert.Multiple(() =>
        {
            Assert.That(ticket["bootstrapPath"]!.GetValue<string>(), Is.EqualTo("/apps/harbor-shop/storefront"));
            Assert.That(html.ContentType, Does.StartWith("text/html"));
            Assert.That(html.Body, Does.Contain("Harbor Mug"));
            Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        });
    }

    [Test]
    public void Handle_cloneAddsAWorkspaceAndResetRestoresTheTemplate()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":"https://git.example/widgets.git","branch":"main"}"""));
        Assert.That(cloned["displayName"]!.GetValue<string>(), Is.EqualTo("widgets"));
        Assert.That(Json(backend, Get("/api/workspaces")).AsArray(), Has.Count.EqualTo(2));

        backend.Reset();

        Assert.That(Json(backend, Get("/api/workspaces")).AsArray(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Handle_unknownPathsReturnNotFound()
    {
        var backend = FakeServerTestComposition.Backend();
        var missing = Run(backend, Get("/api/missing"));

        Assert.That(missing.Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/api/workspaces/missing")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Catalog_matchesTheBuiltInIdentity()
    {
        var backend = FakeServerTestComposition.Backend();
        var catalog = backend.Catalog("fake");

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Id, Is.EqualTo(FakeServerIdentity.Id));
            Assert.That(catalog.Url, Is.EqualTo(FakeServerIdentity.Url));
            Assert.That(catalog.DisplayName, Is.EqualTo(FakeServerIdentity.DisplayName));
            Assert.That(catalog.IsActive, Is.True);
            Assert.That(backend.Matches(FakeServerIdentity.Url), Is.True);
            Assert.That(backend.Matches("http://127.0.0.1:5000"), Is.False);
        });
    }

    [Test]
    public void Handle_gitMutationsAndWorkspaceDeleteSucceed()
    {
        var backend = FakeServerTestComposition.Backend();
        var commit = Json(backend, Post("/api/workspaces/harbor-shop/git/commit"));
        Assert.That(commit["succeeded"]!.GetValue<bool>(), Is.True);

        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Get("/api/workspaces/harbor-shop")).Status, Is.EqualTo(404));
    }

    private static FakeBackendRequestDtoBuilder Get(string path) => new FakeBackendRequestDtoBuilder().Get(path);

    private static FakeBackendRequestDtoBuilder Post(string path, string? body = null)
        => new FakeBackendRequestDtoBuilder().Post(path, body);

    private static AgentUp.Desktop.Features.FakeServer.DTOs.FakeBackendResponseDto Run(
        AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService backend,
        FakeBackendRequestDtoBuilder request)
        => backend.Handle(request.Build());

    private static JsonNode Json(
        AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService backend,
        FakeBackendRequestDtoBuilder request)
        => JsonNode.Parse(Run(backend, request).Body!)!;
}
