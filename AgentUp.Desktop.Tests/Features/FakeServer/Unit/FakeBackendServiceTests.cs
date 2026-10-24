using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Services;
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
        var overview = Json(backend, Get("/api/workspaces/bob/overview"));

        Assert.Multiple(() =>
        {
            Assert.That(workspaces[0]!["id"]!.GetValue<string>(), Is.EqualTo("bob"));
            Assert.That(workspaces[0]!["state"]!.GetValue<string>(), Is.EqualTo("Running"));
            Assert.That(overview["displayName"]!.GetValue<string>(), Is.EqualTo("Bob"));
            Assert.That(overview["applicationCount"]!.GetValue<int>(), Is.EqualTo(2));
        });
    }

    [Test]
    public void Handle_startBeginsTheCheckingLifecycle()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["applications"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["applications"]![0]!["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["state"]!.GetValue<string>(), Is.EqualTo("Starting"));
    }

    [Test]
    public void Handle_startWalksCheckingThenHealthy()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));

        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        var checking = Json(backend, Get("/api/workspaces/bob"));
        Assert.That(checking["healthState"]!.GetValue<string>(), Is.EqualTo("Checking"));
        queued.Dequeue()();
        var healthy = Json(backend, Get("/api/workspaces/bob"));
        Assert.That(healthy["healthState"]!.GetValue<string>(), Is.EqualTo("Healthy"));
        Assert.That(healthy["applications"]!.AsArray(), Has.Count.EqualTo(2));
    }

    [Test]
    public void Handle_gitAndConsoleReturnTheBundledDemo()
    {
        var backend = FakeServerTestComposition.Backend();
        var changes = Json(backend, Get("/api/workspaces/bob/git/changes"));
        var diff = Json(backend, Get("/api/workspaces/bob/git/file").WithQuery("?path=apps/storefront/ProductGrid.tsx"));
        var lines = Json(backend, Get("/api/workspaces/bob/applications/Storefront/output")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(diff["path"]!.GetValue<string>(), Is.EqualTo("apps/storefront/ProductGrid.tsx"));
            Assert.That(lines[3]!["text"]!.GetValue<string>(), Does.Contain("Storefront listening"));
            Assert.That(lines[2]!["stream"]!.GetValue<string>(), Is.EqualTo("stderr"));
        });
    }

    [Test]
    public void Handle_forgeAnswersWhatTheDefinitionSaysTheHostReports()
    {
        var backend = FakeServerTestComposition.Backend();
        var shop = Json(backend, Get("/api/workspaces/bob/forge"));
        var checkout = Json(backend, Get("/api/workspaces/lucy/forge"));
        var docs = Json(backend, Get("/api/workspaces/hector/forge"));

        Assert.Multiple(() =>
        {
            Assert.That(shop["availability"]!.GetValue<string>(), Is.EqualTo("ready"));
            Assert.That(shop["proposal"]!["number"]!.GetValue<string>(), Is.EqualTo("44"));
            Assert.That(shop["mergeability"]!["state"]!.GetValue<string>(), Is.EqualTo("unstable"));
            Assert.That(shop["checks"]!.AsArray(), Has.Count.EqualTo(4), "one passing, one failing, one running, one skipped");
            Assert.That(checkout["proposal"]!["number"]!.GetValue<string>(), Is.EqualTo("44"),
                "the reviewer reads the author's proposal");
            Assert.That(shop["reviews"]!.AsArray().Single()!["state"]!.GetValue<string>(),
                Is.EqualTo("CHANGES_REQUESTED"));
            Assert.That(checkout["threads"]!.AsArray().Count(thread => !thread!["isResolved"]!.GetValue<bool>()), Is.EqualTo(2));
            Assert.That(docs["availability"]!.GetValue<string>(), Is.EqualTo("unreachable"));
            Assert.That(docs["detail"]!.GetValue<string>(), Does.Contain("502"));
        });
    }

    [Test]
    public void Handle_forgeReadsAsNotInstalledWhenTheDefinitionSaysNothing()
    {
        var definition = new FakeServerDefinition(FakeServerTestComposition.Definition().Root.DeepClone());
        definition.Root.AsObject().Remove("forge");
        var hosting = Json(new FakeBackendService(definition), Get("/api/workspaces/bob/forge"));

        Assert.That(hosting["availability"]!.GetValue<string>(), Is.EqualTo("notInstalled"));
        Assert.That(hosting["detail"]!.GetValue<string>(), Does.Contain("forge capability"));
        Assert.That(hosting["remedy"]!.GetValue<string>(), Does.Contain("capabilities bootstrap"));
        Assert.That(hosting["proposal"], Is.Null);
        Assert.That(hosting["checks"]!.AsArray(), Is.Empty);
        Assert.That(hosting["threads"]!.AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_agentLoopReportsTheTaskAndWhatBlocksIt()
    {
        var backend = FakeServerTestComposition.Backend();
        var shop = Json(backend, Get("/api/workspaces/bob/agent"))["loop"]!;
        var checkout = Json(backend, Get("/api/workspaces/lucy/agent"))["loop"]!;
        var docs = Json(backend, Get("/api/workspaces/hector/agent"))["loop"]!;

        Assert.Multiple(() =>
        {
            Assert.That(shop["taskNumber"]!.GetValue<int>(), Is.EqualTo(44));
            Assert.That(shop["active"]!.GetValue<bool>(), Is.True);
            Assert.That(checkout["blockReason"]!.GetValue<string>(), Does.Contain("approve the review"));
            Assert.That(checkout["permissionRuleIds"]!.AsArray()[0]!.GetValue<string>(), Is.EqualTo("forge.submit_review"));
            Assert.That(docs["stopReason"]!.GetValue<string>(), Is.EqualTo("forge.unreachable"));
            Assert.That(docs["active"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public void Handle_agentPromptPublishesScriptedEvents()
    {
        var backend = FakeServerTestComposition.Backend();
        var published = new List<string>();
        using var subscription = backend.SubscribeAgent("bob", item => published.Add(item.Type));

        var result = Run(backend, Post("/api/workspaces/bob/agent/messages", """{"message":"status?"}"""));
        var replayed = backend.AgentEventsAfter("bob", 0);
        var changes = Json(backend, Get("/api/workspaces/bob/git/changes"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(204));
            Assert.That(published, Does.Contain("user_message"));
            Assert.That(published, Does.Contain("session_update"));
            Assert.That(replayed.Select(item => item.Type), Does.Contain("user_message"));
            Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(Json(backend, Get("/api/workspaces/bob/agent"))["state"]!.GetValue<string>(), Is.EqualTo("ready"));
        });
    }

    [Test]
    public void Handle_issuesTicketsAndServesBundledHtml()
    {
        var backend = FakeServerTestComposition.Backend();
        var ticket = Json(backend, Post("/api/apps/tickets", """{"workspaceId":"bob","allocatedPort":9100}"""));
        var html = Run(backend, Get("/apps/bob/storefront"));

        Assert.Multiple(() =>
        {
            Assert.That(ticket["bootstrapPath"]!.GetValue<string>(), Is.EqualTo("/apps/bob/storefront"));
            Assert.That(html.ContentType, Does.StartWith("text/html"));
            Assert.That(html.Body, Does.Contain("Harbor Mug"));
            Assert.That(html.Body, Does.Contain("Place order"));
            Assert.That(html.Body, Does.Not.Contain("bundled with the client"));
            Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        });
    }

    [Test]
    public void Handle_cloneAddsAWorkspaceAndResetRestoresTheTemplate()
    {
        var backend = FakeServerTestComposition.Backend();
        var bundled = Json(backend, Get("/api/workspaces")).AsArray().Count;
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":"https://git.example/widgets.git","branch":"main"}"""));
        Assert.That(cloned["displayName"]!.GetValue<string>(), Is.EqualTo("widgets"));
        Assert.That(Json(backend, Get("/api/workspaces")).AsArray(), Has.Count.EqualTo(bundled + 1));

        backend.Reset();

        Assert.That(Json(backend, Get("/api/workspaces")).AsArray(), Has.Count.EqualTo(bundled));
    }

    [Test]
    public void Handle_refreshUpdatesADerivedCloneAndRejectsOthers()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":"https://git.example/widgets.git","revision":"topic","derived":true}"""));
        var workspaceId = cloned["id"]!.GetValue<string>();

        var refreshed = Json(backend, Post($"/api/source-clones/{workspaceId}/refresh"));
        var rejected = Run(backend, Post("/api/source-clones/bob/refresh"));

        Assert.That(cloned["derived"]!.GetValue<bool>(), Is.True);
        Assert.That(refreshed["commit"]!.GetValue<string>(), Is.EqualTo("refreshed"));
        Assert.That(rejected.Status, Is.EqualTo(400));
    }

    [Test]
    public void Handle_refreshMovesADerivedCloneOntoAnotherRevision()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":"https://git.example/widgets.git","revision":"topic","derived":true}"""));
        var workspaceId = cloned["id"]!.GetValue<string>();

        var refreshed = Json(backend, Post($"/api/source-clones/{workspaceId}/refresh", """{"revision":"release"}"""));

        Assert.That(refreshed["id"]!.GetValue<string>(), Is.EqualTo(workspaceId));
        Assert.That(refreshed["displayName"]!.GetValue<string>(), Is.EqualTo("Review · release"));
        Assert.That(refreshed["sourceRevision"]!.GetValue<string>(), Is.EqualTo("release"));
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
    public void Handle_gitCommitRemovesSelectedFiles()
    {
        var backend = FakeServerTestComposition.Backend();
        var commit = Json(backend, Post("/api/workspaces/bob/git/commit",
            """{"files":["apps/storefront/ProductGrid.tsx"],"message":"fix(storefront): featured grid"}"""));
        var changes = Json(backend, Get("/api/workspaces/bob/git/changes"));
        Assert.That(commit["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(commit["head"]!["ahead"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(commit["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(1));

        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/bob")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Get("/api/workspaces/bob")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_emptyPathAndRootFallThroughToNotFound()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Get("")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_workspaceEventsAndTutorialCleanup()
    {
        var backend = FakeServerTestComposition.Backend();
        var events = Run(backend, Get("/api/workspaces/events"));

        Assert.That(events.Status, Is.EqualTo(200));
        Assert.That(events.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(events.Body, Does.Contain("bob"));
        Assert.That(events.KeepOpen, Is.True);
        Assert.That(Run(backend, Post("/api/workspaces/tutorial/cleanup")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/audit/record", "{}")).Status, Is.EqualTo(204));
    }

    [Test]
    public void Handle_capabilityModulesListFirstPartyPackages()
    {
        var backend = FakeServerTestComposition.Backend();
        var listed = Json(backend, Get("/api/capabilities")).AsArray();
        var disabled = Json(backend, Post("/api/capabilities/disable/dotnet"));
        var enabled = Json(backend, Post("/api/capabilities/enable", """{"id":"dotnet"}"""));

        Assert.That(listed.Select(item => item!["id"]!.GetValue<string>()), Is.EqualTo(new[] { "dotnet", "docker", "codex", "cursor", "claude" }));
        Assert.That(disabled["enabled"]!.GetValue<bool>(), Is.False);
        Assert.That(enabled["enabled"]!.GetValue<bool>(), Is.True);
        Assert.That(enabled["canRun"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void Handle_agentListFollowsEnabledCapabilities()
    {
        var backend = FakeServerTestComposition.Backend();
        var listed = Json(backend, Get("/api/workspaces/bob/agent"));
        Json(backend, Post("/api/capabilities/disable/claude"));
        var afterDisable = Json(backend, Get("/api/workspaces/bob/agent"));
        var rejected = Run(backend, Post("/api/workspaces/bob/agent", """{"agent":"claude"}"""));
        Json(backend, Post("/api/capabilities/enable", """{"id":"claude"}"""));
        var scheduled = Json(backend, Post("/api/workspaces/bob/agent", """{"agent":"claude"}"""));

        Assert.That(listed["agents"]!.AsArray().Select(item => item!["agent"]!.GetValue<string>()), Is.EqualTo(new[] { "codex", "cursor", "claude" }));
        var available = listed["agents"]!.AsArray().Single(item => item!["agent"]!.GetValue<string>() == "codex")!;
        Assert.That(available["available"]!.GetValue<bool>(), Is.True);
        Assert.That(available["unavailability"], Is.Null);
        var disabled = afterDisable["agents"]!.AsArray().Single(item => item!["agent"]!.GetValue<string>() == "claude")!;
        Assert.That(disabled["available"]!.GetValue<bool>(), Is.False);
        Assert.That(disabled["unavailability"]!.GetValue<string>(), Is.EqualTo("capabilityNotEnabled"));
        Assert.That(disabled["remedy"]!.GetValue<string>(), Does.Contain("claude"));
        Assert.That(rejected.Status, Is.EqualTo(409));
        Assert.That(scheduled["agent"]!.GetValue<string>(), Is.EqualTo("claude"));
    }

    [Test]
    public void Handle_agentListReportsExecutableNotFoundWhenEnabledModuleCannotRun()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{"id":"bob","displayName":"Bob","state":"Running","applications":[]}],"agents":{"bob":{"session":{"agent":"claude","state":"ready"}}},"capabilities":[{"id":"claude","displayName":"Claude","kind":"agent","enabled":true,"canRun":false}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var session = Json(backend, Get("/api/workspaces/bob/agent"));
        var claude = session["agents"]!.AsArray().Single()!;
        var rejected = Run(backend, Post("/api/workspaces/bob/agent", """{"agent":"claude"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(claude["available"]!.GetValue<bool>(), Is.False);
            Assert.That(claude["unavailability"]!.GetValue<string>(), Is.EqualTo("executableNotFound"));
            Assert.That(claude["detail"]!.GetValue<string>(), Does.Contain("claude"));
            Assert.That(claude["remedy"]!.GetValue<string>(), Does.Contain("PATH"));
            Assert.That(session["agent"], Is.Null);
            Assert.That(rejected.Status, Is.EqualTo(409));
            Assert.That(rejected.Body, Does.Contain("not enabled"));
        });
    }

    [Test]
    public void Handle_gitLogQueueFetchAndMissingDiff()
    {
        var backend = FakeServerTestComposition.Backend();
        var log = Run(backend, Get("/api/workspaces/bob/git/log"));
        var queue = Run(backend, Get("/api/workspaces/bob/commit-queue"));
        var fetch = Json(backend, Post("/api/workspaces/bob/git/fetch"));
        var missing = Run(backend, Get("/api/workspaces/bob/git/file"));

        Assert.That(log.Status, Is.EqualTo(200).Or.EqualTo(404));
        Assert.That(queue.Status, Is.EqualTo(200).Or.EqualTo(404));
        Assert.That(fetch["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(missing.Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_gitPullIntegratesIncomingCommitOnce()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Post("/api/workspaces/bob/git/fetch"));
        Json(backend, Post("/api/workspaces/bob/git/pull"));
        var secondFetch = Json(backend, Post("/api/workspaces/bob/git/fetch"));
        var secondPull = Json(backend, Post("/api/workspaces/bob/git/pull"));
        var log = Json(backend, Get("/api/workspaces/bob/git/log"));

        Assert.That(secondFetch["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(secondPull["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(
            log["commits"]!.AsArray().Count(item => item!["author"]!.GetValue<string>() == "origin"),
            Is.EqualTo(1));
    }

    [Test]
    public void Handle_bindAgentRecordsTheWorkspaceAgent()
    {
        var backend = FakeServerTestComposition.Backend();
        var bound = Json(backend, new FakeBackendRequestDtoBuilder().Put("/api/workspaces/bob/agent/binding", "{\"agent\":\"codex\"}"));

        Assert.That(bound["boundAgent"]!.GetValue<string>(), Is.EqualTo("codex"));
    }

    [Test]
    public void Handle_bindAgentUnknownWorkspaceReturnsNotFound()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(
            Run(backend, new FakeBackendRequestDtoBuilder().Put("/api/workspaces/missing/agent/binding", "{\"agent\":\"codex\"}")).Status,
            Is.EqualTo(404));
    }

    [Test]
    public void Handle_bindAgentEmptyKeepsTheSession()
    {
        var backend = FakeServerTestComposition.Backend();
        var session = Json(backend, new FakeBackendRequestDtoBuilder().Put("/api/workspaces/bob/agent/binding", "{\"agent\":\"\"}"));

        Assert.That(session["boundAgent"]!.GetValue<string>(), Is.EqualTo("Codex"));
    }

    [Test]
    public void Handle_autonomyRoutesReturnNoContent()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(
            Run(backend, new FakeBackendRequestDtoBuilder().Put("/api/workspaces/bob/agent/queue-order", "{\"order\":\"NumberAsc\"}")).Status,
            Is.EqualTo(204));
    }

    [Test]
    public void Handle_agentControlAndEmptyCollections()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/bob/agent")).Status, Is.EqualTo(200));
        Assert.That(Run(backend, Post("/api/workspaces/bob/agent/permissions", "{}")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/bob/agent")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/bob/applications/Storefront/metrics")).AsArray(), Is.Empty);
        Assert.That(Json(backend, Get("/api/workspaces/bob/applications/Storefront/validation-flows")).AsArray(), Is.Empty);
        Assert.That(Json(backend, Get("/api/workspaces/bob/applications/Storefront/database/tables")).AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_viewerTicketDatabaseQueryAndCloneDefaults()
    {
        var backend = FakeServerTestComposition.Backend();
        var ticket = Json(backend, Post("/api/workspaces/bob/applications/Storefront/viewer-ticket"));
        var query = Json(backend, Post("/api/workspaces/bob/applications/Storefront/database/query", "{}"));
        var cloned = Json(backend, Post("/api/source-clones"));

        Assert.That(ticket["viewerUrl"]!.GetValue<string>(), Does.Contain("storefront"));
        Assert.That(query["rows"]!.AsArray(), Is.Empty);
        Assert.That(cloned["branch"]!.GetValue<string>(), Is.EqualTo("main"));
        Assert.That(cloned["displayName"]!.GetValue<string>(), Is.EqualTo("demo"));
    }

    [Test]
    public void Handle_rejectsIncompleteTicketsAndUnknownPages()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/apps/tickets", "{}")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/apps/tickets")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/apps/tickets", """{"workspaceId":"bob","allocatedPort":1}""")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/apps/bob")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/apps/bob/missing")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/bob/agent/messages", "not-json")).Status, Is.EqualTo(204));
    }

    [Test]
    public void Handle_gitPullPushAndConsoleShortPath()
    {
        var backend = FakeServerTestComposition.Backend();
        var pull = Json(backend, Post("/api/workspaces/bob/git/pull"));
        var push = Json(backend, Post("/api/workspaces/bob/git/push"));
        var other = Json(backend, Post("/api/workspaces/bob/git/status"));
        var emptyConsole = Json(backend, Get("/api/workspaces/bob/app/output"));

        Assert.That(pull["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("feat/44-restock"));
        Assert.That(push["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(other["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(emptyConsole.AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_agentEventsReplayAndUnknownWorkspace()
    {
        var backend = FakeServerTestComposition.Backend();
        Run(backend, Post("/api/workspaces/bob/agent/messages", """{"message":"hi"}"""));
        var stream = Run(backend, Get("/api/workspaces/bob/agent/events").WithQuery("?after=0"));
        var later = Run(backend, Get("/api/workspaces/bob/agent/events").WithQuery("after=99"));

        Assert.That(stream.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(stream.Body, Does.Contain("user_message"));
        Assert.That(later.Body, Is.Empty);
        Assert.That(backend.AgentEventsAfter("missing", 0), Is.Empty);
        Assert.That(Run(backend, Get("/api/workspaces/missing/overview")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/api/workspaces/missing/git/changes")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Subscribe_workspaceAndAgentListenersCanDisposeTwice()
    {
        var backend = FakeServerTestComposition.Backend();
        var snapshots = 0;
        var agentHits = 0;
        var workspaces = backend.SubscribeWorkspaces(_ => snapshots++);
        var agent = backend.SubscribeAgent("bob", _ => agentHits++);

        Run(backend, Post("/api/workspaces/bob/stop"));
        Run(backend, Post("/api/workspaces/bob/agent/messages", """{"message":"hi"}"""));
        workspaces.Dispose();
        workspaces.Dispose();
        agent.Dispose();
        agent.Dispose();

        Assert.That(snapshots, Is.GreaterThan(0));
        Assert.That(agentHits, Is.GreaterThan(0));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
    }

    [Test]
    public void Handle_queryWithoutQuestionMarkAndInvalidTicketPort()
    {
        var backend = FakeServerTestComposition.Backend();
        var diff = Json(backend, Get("/api/workspaces/bob/git/file").WithQuery("path=apps/storefront/ProductGrid.tsx"));
        var ticket = Run(backend, Post("/api/apps/tickets", """{"workspaceId":"bob","allocatedPort":"9100"}"""));

        Assert.That(diff["path"]!.GetValue<string>(), Is.EqualTo("apps/storefront/ProductGrid.tsx"));
        Assert.That(ticket.Status, Is.EqualTo(404));
        Assert.That(backend.Matches((Uri?)null), Is.False);
        Assert.That(backend.ApplicationHtml("storefront"), Does.Contain("Harbor Shop"));
        Assert.That(backend.ApplicationHtml("missing"), Is.Null);
    }

    [Test]
    public void Handle_skipsNonObjectWorkspaceEventEntries()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":["skip",{"id":"bob","displayName":"Bob","state":"Running","applications":[]}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var events = Run(backend, Get("/api/workspaces/events"));

        Assert.That(events.Body, Does.Contain("bob"));
        Assert.That(events.Body, Does.Not.Contain("skip"));
    }

    [Test]
    public void Handle_gitHeadDiscardAndBranchMutations()
    {
        var backend = FakeServerTestComposition.Backend();
        var head = Json(backend, Get("/api/workspaces/bob/git/head"));
        var discarded = Json(backend, Post("/api/workspaces/bob/git/discard",
            """{"files":["apps/api/orders.ts"]}"""));
        var created = Json(backend, Post("/api/workspaces/bob/git/branch",
            """{"name":"topic","create":true}"""));
        var empty = Json(backend, Post("/api/workspaces/bob/git/branch", """{"name":" "}"""));

        Assert.That(head["branch"]!.GetValue<string>(), Is.EqualTo("feat/44-restock"));
        Assert.That(discarded["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(created["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("topic"));
        Assert.That(empty["succeeded"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Handle_gitCheckoutAndInvalidCommitBodies()
    {
        var backend = FakeServerTestComposition.Backend();
        var remote = Json(backend, Post("/api/workspaces/bob/git/checkout", """{"name":"origin/release"}"""));
        var emptyCommit = Json(backend, Post("/api/workspaces/bob/git/commit"));
        var invalidFiles = Json(backend, Post("/api/workspaces/bob/git/commit", """{"files":"nope","message":1}"""));
        var invalidCreate = Json(backend, Post("/api/workspaces/bob/git/branch", """{"name":"topic","create":"yes"}"""));

        Assert.That(remote["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("release"));
        Assert.That(emptyCommit["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(invalidFiles["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(invalidCreate["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(Json(backend, Post("/api/workspaces/bob/git/commit", """{"files":[1],"message":"x"}"""))["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(Json(backend, Post("/api/workspaces/bob/git/branch"))["succeeded"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Handle_gitAndAgentRoutesOnClonedWorkspacesReturnNotFound()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones"));
        var id = cloned["id"]!.GetValue<string>();

        Assert.That(Run(backend, Get($"/api/workspaces/{id}/git/head")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get($"/api/workspaces/{id}/git/file").WithQuery("?path=README.md")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post($"/api/workspaces/{id}/git/fetch")).Status, Is.EqualTo(404));
        Assert.That(Json(backend, Get($"/api/workspaces/{id}/overview"))["applicationCount"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(Run(backend, Post($"/api/workspaces/{id}/agent/messages", """{"message":"hi"}""")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_missingWorkspaceAndCapabilityIdsReturnNotFound()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/missing/start")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/missing/stop")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/api/workspaces/missing/agent")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/missing/agent", """{"agent":"claude"}""")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/missing/git/push")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/capabilities/enable", """{"id":"missing"}""")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/capabilities/disable/missing")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/capabilities/enable", "{}")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_startThenStopCancelsTheCheckingPhase()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));

        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["healthState"], Is.Null);
    }

    [Test]
    public void Handle_deletingAStartingWorkspaceDropsTheScheduledPhase()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));

        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/bob")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Run(backend, Get("/api/workspaces/bob")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_disablingEveryAgentClearsTheSelectedAgent()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Post("/api/capabilities/disable/codex"));
        Json(backend, Post("/api/capabilities/disable/cursor"));
        Json(backend, Post("/api/capabilities/disable/claude"));
        var session = Json(backend, Get("/api/workspaces/bob/agent"));

        Assert.That(session["agents"]!.AsArray().Select(item => item!["available"]!.GetValue<bool>()), Is.All.False);
        Assert.That(session["agent"], Is.Null);
    }

    [Test]
    public void Handle_exposesLoopStateOnTheAgentSession()
    {
        var backend = FakeServerTestComposition.Backend();
        var session = Json(backend, Get("/api/workspaces/bob/agent"));

        Assert.Multiple(() =>
        {
            Assert.That(session["loop"]!["phase"]!.GetValue<string>(), Is.EqualTo("running"));
            Assert.That(session["loop"]!["taskTitle"]!.GetValue<string>(), Is.EqualTo("Restock featured products"));
            Assert.That(session["loop"]!["stopReason"], Is.Null);
            Assert.That(session["boundAgent"]!.GetValue<string>(), Is.EqualTo("Codex"));
        });
    }

    [Test]
    public void Handle_autonomyChainEnableThenActivateExposesATypedTerminal()
    {
        var backend = FakeServerTestComposition.Backend((_, work) => work());
        var before = Json(backend, Get("/api/autonomy"));
        Assert.That(before["enabled"]!.GetValue<bool>(), Is.False);
        Assert.That(Run(backend, Put("/api/autonomy", """{"enabled":true}""")).Status, Is.EqualTo(204));
        var enabled = Json(backend, Get("/api/autonomy"));
        Assert.That(enabled["enabled"]!.GetValue<bool>(), Is.True);
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(204));
        var loop = Json(backend, Get("/api/workspaces/bob/agent/loop"));
        Assert.That(loop["terminal"]!.GetValue<string>(), Is.EqualTo("Completed"));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":false}""")).Status, Is.EqualTo(204));
        var cancelled = Json(backend, Get("/api/workspaces/bob/agent/loop"));
        Assert.That(cancelled["terminal"]!.GetValue<string>(), Is.EqualTo("Cancelled"));
    }

    [Test]
    public void Handle_ignoresAStaleLoopCompletionAfterALaterActivation()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));
        Assert.That(Run(backend, Put("/api/autonomy", """{"enabled":true}""")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/bob/agent/loop"))["active"]!.GetValue<bool>(), Is.True);
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/bob/agent/loop"))["terminal"]!.GetValue<string>(), Is.EqualTo("Completed"));
    }

    [Test]
    public void Handle_ignoresLoopCompletionAfterCancel()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));
        Assert.That(Run(backend, Put("/api/autonomy", """{"enabled":true}""")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/workspaces/bob/agent/autonomy", """{"active":false}""")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/bob/agent/loop"))["terminal"]!.GetValue<string>(), Is.EqualTo("Cancelled"));
    }

    [Test]
    public void Handle_autonomyLoopRoutesReturnNotFoundForUnknownWorkspaces()
    {
        var backend = FakeServerTestComposition.Backend();
        Assert.That(Run(backend, Get("/api/workspaces/missing/agent/loop")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Put("/api/workspaces/missing/agent/autonomy", """{"active":true}""")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_autonomyEnablementReadsJsonBooleansAndResets()
    {
        var backend = FakeServerTestComposition.Backend();
        Assert.That(Run(backend, Put("/api/autonomy", "")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/autonomy"))["enabled"]!.GetValue<bool>(), Is.False);
        Assert.That(Run(backend, Put("/api/autonomy", "{not-json")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Put("/api/autonomy", """{"enabled":true}""")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/autonomy"))["enabled"]!.GetValue<bool>(), Is.True);
        backend.Reset();
        Assert.That(Json(backend, Get("/api/autonomy"))["enabled"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Handle_autonomyMarksAgentAvailableFalseWhenEveryAgentIsDisabled()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Post("/api/capabilities/disable/codex"));
        Json(backend, Post("/api/capabilities/disable/cursor"));
        Json(backend, Post("/api/capabilities/disable/claude"));
        var status = Json(backend, Get("/api/autonomy"));
        var agent = status["preconditions"]!.AsArray()
            .Single(item => item!["id"]!.GetValue<string>() == "agentAvailable")!;

        Assert.That(agent["satisfied"]!.GetValue<bool>(), Is.False);
        Assert.That(agent["remedy"]!.GetValue<string>(), Is.EqualTo("Enable an agent capability"));
    }

    [Test]
    public void Handle_agentSessionListsCurrentAndPriorSavedSessions()
    {
        var backend = FakeServerTestComposition.Backend();
        var sessions = Json(backend, Get("/api/workspaces/bob/agent"))["sessions"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(sessions, Has.Count.EqualTo(2));
            Assert.That(sessions[0]!["sessionId"]!.GetValue<string>(), Is.EqualTo("demo-agent"));
            Assert.That(sessions[1]!["sessionId"]!.GetValue<string>(), Is.EqualTo("demo-prior"));
            Assert.That(sessions[1]!["agent"]!.GetValue<string>(), Is.EqualTo("Claude"));
        });
    }

    [Test]
    public void Handle_resumeMakesTheSavedSessionCurrent()
    {
        var backend = FakeServerTestComposition.Backend();
        var resumed = Json(backend, Post("/api/workspaces/bob/agent/sessions/demo-prior/resume"));

        Assert.Multiple(() =>
        {
            Assert.That(resumed["sessionId"]!.GetValue<string>(), Is.EqualTo("demo-prior"));
            Assert.That(resumed["agent"]!.GetValue<string>(), Is.EqualTo("claude"));
            Assert.That(resumed["state"]!.GetValue<string>(), Is.EqualTo("ready"));
            Assert.That(resumed["boundAgent"]!.GetValue<string>(), Is.EqualTo("Codex"));
            Assert.That(Run(backend, Post("/api/workspaces/bob/agent/sessions/missing/resume")).Status, Is.EqualTo(404));
        });
    }

    [Test]
    public void Handle_resumeReturnsNotFoundWhenTheWorkspaceOrPathIsInvalid()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/missing/agent/sessions/demo-prior/resume")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/bob/agent/sessions/resume")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/bob/agent/sessions//resume")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_reviewSnapshotExportsTheBundledWorkspace()
    {
        var backend = FakeServerTestComposition.Backend();
        var snapshot = Json(backend, Get("/api/workspaces/bob/review-snapshot"));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot["workspaceId"]!.GetValue<string>(), Is.EqualTo("bob"));
            Assert.That(snapshot["displayName"]!.GetValue<string>(), Is.EqualTo("Bob"));
            Assert.That(
                snapshot["applications"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()),
                Is.EqualTo(new[] { "Storefront", "Orders API" }));
            Assert.That(snapshot["verification"], Is.Null);
            Assert.That(Run(backend, Get("/api/workspaces/missing/review-snapshot")).Status, Is.EqualTo(404));
        });
    }

    [Test]
    public void Handle_reviewSnapshotDefaultsKindAndProtocolWhenTheyAreOmitted()
    {
        const string json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{"id":"bob","displayName":"Bob","state":"Running","applications":["skip",{"name":"Storefront","state":"Running","allocatedPorts":["skip",{"variable":"WEB_PORT","allocatedPort":9100}]}]}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var snapshot = Json(backend, Get("/api/workspaces/bob/review-snapshot"));
        var app = snapshot["applications"]!.AsArray().Single()!.AsObject();
        var port = app["ports"]!.AsArray().Single()!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(app["kind"]!.GetValue<string>(), Is.EqualTo("Process"));
            Assert.That(port["protocol"]!.GetValue<string>(), Is.EqualTo("http"));
            Assert.That(port["allocatedPort"]!.GetValue<int>(), Is.EqualTo(9100));
        });
    }

    [Test]
    public void Handle_cloneEmptyRepositoryAndOddQueries()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":""}"""));
        var events = Run(backend, Get("/api/workspaces/bob/agent/events").WithQuery("after=nope"));
        var file = Run(backend, Get("/api/workspaces/bob/git/file").WithQuery("path"));

        Assert.That(cloned["displayName"]!.GetValue<string>(), Is.EqualTo("cloned"));
        Assert.That(events.Body, Is.Empty);
        Assert.That(file.Status, Is.EqualTo(404));
        Assert.That(backend.ApplicationHtml(1), Is.Null);
        Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        Assert.That(Run(backend, Get("/api/workspaces/bob/unknown")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/api/workspaces/tutorial/status")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_skipsWorkspacesWithoutIdsWhenCapturingTemplates()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{},{"id":"bob","displayName":"Bob","state":"Stopped","applications":[]}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));

        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["applications"]!.AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_checkingPhaseIgnoresAWorkspaceThatLeftStarting()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));
        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/bob/start")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Run(backend, Post("/api/workspaces/bob/stop")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/bob"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
    }

    [Test]
    public void Handle_agentPromptWithoutGitStillReplies()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{"id":"bob","displayName":"Bob","state":"Running","applications":[]}],"agents":{"bob":{"session":{"agent":"claude","state":"ready"}}}}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var published = new List<string>();
        using var subscription = backend.SubscribeAgent("bob", item => published.Add(item.Type));

        Assert.That(Run(backend, Post("/api/workspaces/bob/agent/messages", """{"message":"hi"}""")).Status, Is.EqualTo(204));
        Assert.That(published, Does.Contain("session_update"));
        Assert.That(backend.AgentEventsAfter("bob", 0).Select(item => item.Payload.ToJsonString()),
            Has.Some.Contain("Harbor is running locally."));
    }

    private static FakeBackendRequestDtoBuilder Get(string path) => new FakeBackendRequestDtoBuilder().Get(path);

    private static FakeBackendRequestDtoBuilder Post(string path, string? body = null)
        => new FakeBackendRequestDtoBuilder().Post(path, body);

    private static FakeBackendRequestDtoBuilder Put(string path, string? body = null)
        => new FakeBackendRequestDtoBuilder().Put(path, body);

    private static AgentUp.Desktop.Features.FakeServer.DTOs.FakeBackendResponseDto Run(
        AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService backend,
        FakeBackendRequestDtoBuilder request)
        => backend.Handle(request.Build());

    private static JsonNode Json(
        AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService backend,
        FakeBackendRequestDtoBuilder request)
        => JsonNode.Parse(Run(backend, request).Body!)!;
}
