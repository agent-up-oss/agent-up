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
    public void Handle_startBeginsTheCheckingLifecycle()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["applications"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["applications"]![0]!["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Starting"));
    }

    [Test]
    public void Handle_startWalksCheckingThenHealthy()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        var checking = Json(backend, Get("/api/workspaces/harbor-shop"));
        Assert.That(checking["healthState"]!.GetValue<string>(), Is.EqualTo("Checking"));
        queued.Dequeue()();
        var healthy = Json(backend, Get("/api/workspaces/harbor-shop"));
        Assert.That(healthy["healthState"]!.GetValue<string>(), Is.EqualTo("Healthy"));
        Assert.That(healthy["applications"]!.AsArray(), Has.Count.EqualTo(2));
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
        var changes = Json(backend, Get("/api/workspaces/harbor-shop/git/changes"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(204));
            Assert.That(published, Does.Contain("user_message"));
            Assert.That(published, Does.Contain("session_update"));
            Assert.That(replayed.Select(item => item.Type), Does.Contain("user_message"));
            Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(3));
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
            Assert.That(html.Body, Does.Contain("Place order"));
            Assert.That(html.Body, Does.Not.Contain("bundled with the client"));
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
    public void Handle_gitCommitRemovesSelectedFiles()
    {
        var backend = FakeServerTestComposition.Backend();
        var commit = Json(backend, Post("/api/workspaces/harbor-shop/git/commit",
            """{"files":["apps/storefront/ProductGrid.tsx"],"message":"fix(storefront): featured grid"}"""));
        var changes = Json(backend, Get("/api/workspaces/harbor-shop/git/changes"));
        Assert.That(commit["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(commit["head"]!["ahead"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(commit["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(changes["fileCount"]!.GetValue<int>(), Is.EqualTo(1));

        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Get("/api/workspaces/harbor-shop")).Status, Is.EqualTo(404));
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
        Assert.That(events.Body, Does.Contain("harbor-shop"));
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
        var listed = Json(backend, Get("/api/workspaces/harbor-shop/agent"));
        Json(backend, Post("/api/capabilities/disable/claude"));
        var afterDisable = Json(backend, Get("/api/workspaces/harbor-shop/agent"));
        var rejected = Run(backend, Post("/api/workspaces/harbor-shop/agent", """{"agent":"claude"}"""));
        Json(backend, Post("/api/capabilities/enable", """{"id":"claude"}"""));
        var scheduled = Json(backend, Post("/api/workspaces/harbor-shop/agent", """{"agent":"claude"}"""));

        Assert.That(listed["agents"]!.AsArray().Select(item => item!["agent"]!.GetValue<string>()), Is.EqualTo(new[] { "codex", "cursor", "claude" }));
        Assert.That(afterDisable["agents"]!.AsArray().Select(item => item!["agent"]!.GetValue<string>()), Is.EqualTo(new[] { "codex", "cursor" }));
        Assert.That(rejected.Status, Is.EqualTo(409));
        Assert.That(scheduled["agent"]!.GetValue<string>(), Is.EqualTo("claude"));
    }

    [Test]
    public void Handle_gitLogQueueFetchAndMissingDiff()
    {
        var backend = FakeServerTestComposition.Backend();
        var log = Run(backend, Get("/api/workspaces/harbor-shop/git/log"));
        var queue = Run(backend, Get("/api/workspaces/harbor-shop/commit-queue"));
        var fetch = Json(backend, Post("/api/workspaces/harbor-shop/git/fetch"));
        var missing = Run(backend, Get("/api/workspaces/harbor-shop/git/file"));

        Assert.That(log.Status, Is.EqualTo(200).Or.EqualTo(404));
        Assert.That(queue.Status, Is.EqualTo(200).Or.EqualTo(404));
        Assert.That(fetch["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(missing.Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_gitPullIntegratesIncomingCommitOnce()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Post("/api/workspaces/harbor-shop/git/fetch"));
        Json(backend, Post("/api/workspaces/harbor-shop/git/pull"));
        var secondFetch = Json(backend, Post("/api/workspaces/harbor-shop/git/fetch"));
        var secondPull = Json(backend, Post("/api/workspaces/harbor-shop/git/pull"));
        var log = Json(backend, Get("/api/workspaces/harbor-shop/git/log"));

        Assert.That(secondFetch["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(secondPull["head"]!["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(
            log["commits"]!.AsArray().Count(item => item!["author"]!.GetValue<string>() == "origin"),
            Is.EqualTo(1));
    }

    [Test]
    public void Handle_agentControlAndEmptyCollections()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/agent")).Status, Is.EqualTo(200));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/agent/permissions", "{}")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop/agent")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop/applications/Storefront/metrics")).AsArray(), Is.Empty);
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop/applications/Storefront/validation-flows")).AsArray(), Is.Empty);
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop/applications/Storefront/database/tables")).AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_viewerTicketDatabaseQueryAndCloneDefaults()
    {
        var backend = FakeServerTestComposition.Backend();
        var ticket = Json(backend, Post("/api/workspaces/harbor-shop/applications/Storefront/viewer-ticket"));
        var query = Json(backend, Post("/api/workspaces/harbor-shop/applications/Storefront/database/query", "{}"));
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
        Assert.That(Run(backend, Post("/api/apps/tickets", """{"workspaceId":"harbor-shop","allocatedPort":1}""")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/apps/harbor-shop")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/apps/harbor-shop/missing")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/agent/messages", "not-json")).Status, Is.EqualTo(204));
    }

    [Test]
    public void Handle_gitPullPushAndConsoleShortPath()
    {
        var backend = FakeServerTestComposition.Backend();
        var pull = Json(backend, Post("/api/workspaces/harbor-shop/git/pull"));
        var push = Json(backend, Post("/api/workspaces/harbor-shop/git/push"));
        var other = Json(backend, Post("/api/workspaces/harbor-shop/git/status"));
        var emptyConsole = Json(backend, Get("/api/workspaces/harbor-shop/app/output"));

        Assert.That(pull["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("main"));
        Assert.That(push["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(other["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(emptyConsole.AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_agentEventsReplayAndUnknownWorkspace()
    {
        var backend = FakeServerTestComposition.Backend();
        Run(backend, Post("/api/workspaces/harbor-shop/agent/messages", """{"message":"hi"}"""));
        var stream = Run(backend, Get("/api/workspaces/harbor-shop/agent/events").WithQuery("?after=0"));
        var later = Run(backend, Get("/api/workspaces/harbor-shop/agent/events").WithQuery("after=99"));

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
        var agent = backend.SubscribeAgent("harbor-shop", _ => agentHits++);

        Run(backend, Post("/api/workspaces/harbor-shop/stop"));
        Run(backend, Post("/api/workspaces/harbor-shop/agent/messages", """{"message":"hi"}"""));
        workspaces.Dispose();
        workspaces.Dispose();
        agent.Dispose();
        agent.Dispose();

        Assert.That(snapshots, Is.GreaterThan(0));
        Assert.That(agentHits, Is.GreaterThan(0));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
    }

    [Test]
    public void Handle_queryWithoutQuestionMarkAndInvalidTicketPort()
    {
        var backend = FakeServerTestComposition.Backend();
        var diff = Json(backend, Get("/api/workspaces/harbor-shop/git/file").WithQuery("path=apps/storefront/ProductGrid.tsx"));
        var ticket = Run(backend, Post("/api/apps/tickets", """{"workspaceId":"harbor-shop","allocatedPort":"9100"}"""));

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
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":["skip",{"id":"harbor-shop","displayName":"Harbor Shop","state":"Running","applications":[]}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var events = Run(backend, Get("/api/workspaces/events"));

        Assert.That(events.Body, Does.Contain("harbor-shop"));
        Assert.That(events.Body, Does.Not.Contain("skip"));
    }

    [Test]
    public void Handle_gitHeadDiscardAndBranchMutations()
    {
        var backend = FakeServerTestComposition.Backend();
        var head = Json(backend, Get("/api/workspaces/harbor-shop/git/head"));
        var discarded = Json(backend, Post("/api/workspaces/harbor-shop/git/discard",
            """{"files":["apps/api/orders.ts"]}"""));
        var created = Json(backend, Post("/api/workspaces/harbor-shop/git/branch",
            """{"name":"topic","create":true}"""));
        var empty = Json(backend, Post("/api/workspaces/harbor-shop/git/branch", """{"name":" "}"""));

        Assert.That(head["branch"]!.GetValue<string>(), Is.EqualTo("main"));
        Assert.That(discarded["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(created["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("topic"));
        Assert.That(empty["succeeded"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Handle_gitCheckoutAndInvalidCommitBodies()
    {
        var backend = FakeServerTestComposition.Backend();
        var remote = Json(backend, Post("/api/workspaces/harbor-shop/git/checkout", """{"name":"origin/release"}"""));
        var emptyCommit = Json(backend, Post("/api/workspaces/harbor-shop/git/commit"));
        var invalidFiles = Json(backend, Post("/api/workspaces/harbor-shop/git/commit", """{"files":"nope","message":1}"""));
        var invalidCreate = Json(backend, Post("/api/workspaces/harbor-shop/git/branch", """{"name":"topic","create":"yes"}"""));

        Assert.That(remote["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("release"));
        Assert.That(emptyCommit["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(invalidFiles["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(invalidCreate["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(Json(backend, Post("/api/workspaces/harbor-shop/git/commit", """{"files":[1],"message":"x"}"""))["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(Json(backend, Post("/api/workspaces/harbor-shop/git/branch"))["succeeded"]!.GetValue<bool>(), Is.False);
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

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["healthState"], Is.Null);
    }

    [Test]
    public void Handle_deletingAStartingWorkspaceDropsTheScheduledPhase()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, new FakeBackendRequestDtoBuilder().Delete("/api/workspaces/harbor-shop")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Run(backend, Get("/api/workspaces/harbor-shop")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_disablingEveryAgentClearsTheSelectedAgent()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Post("/api/capabilities/disable/codex"));
        Json(backend, Post("/api/capabilities/disable/cursor"));
        Json(backend, Post("/api/capabilities/disable/claude"));
        var session = Json(backend, Get("/api/workspaces/harbor-shop/agent"));

        Assert.That(session["agents"]!.AsArray(), Is.Empty);
        Assert.That(session["agent"], Is.Null);
    }

    [Test]
    public void Handle_cloneEmptyRepositoryAndOddQueries()
    {
        var backend = FakeServerTestComposition.Backend();
        var cloned = Json(backend, Post("/api/source-clones", """{"repository":""}"""));
        var events = Run(backend, Get("/api/workspaces/harbor-shop/agent/events").WithQuery("after=nope"));
        var file = Run(backend, Get("/api/workspaces/harbor-shop/git/file").WithQuery("path"));

        Assert.That(cloned["displayName"]!.GetValue<string>(), Is.EqualTo("cloned"));
        Assert.That(events.Body, Is.Empty);
        Assert.That(file.Status, Is.EqualTo(404));
        Assert.That(backend.ApplicationHtml(1), Is.Null);
        Assert.That(backend.ApplicationHtml(9100), Does.Contain("Harbor Shop"));
        Assert.That(Run(backend, Get("/api/workspaces/harbor-shop/unknown")).Status, Is.EqualTo(404));
        Assert.That(Run(backend, Get("/api/workspaces/tutorial/status")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Handle_skipsWorkspacesWithoutIdsWhenCapturingTemplates()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{},{"id":"harbor-shop","displayName":"Harbor Shop","state":"Stopped","applications":[]}]}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["applications"]!.AsArray(), Is.Empty);
    }

    [Test]
    public void Handle_checkingPhaseIgnoresAWorkspaceThatLeftStarting()
    {
        var queued = new Queue<Action>();
        var backend = FakeServerTestComposition.Backend((_, work) => queued.Enqueue(work));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/start")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/stop")).Status, Is.EqualTo(204));
        queued.Dequeue()();
        Assert.That(Json(backend, Get("/api/workspaces/harbor-shop"))["state"]!.GetValue<string>(), Is.EqualTo("Stopped"));
    }

    [Test]
    public void Handle_agentPromptWithoutGitStillReplies()
    {
        var json = """
            {"id":"fake","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[{"id":"harbor-shop","displayName":"Harbor Shop","state":"Running","applications":[]}],"agents":{"harbor-shop":{"session":{"agent":"claude","state":"ready"}}}}
            """;
        var backend = new AgentUp.Desktop.Features.FakeServer.Services.FakeBackendService(
            new AgentUp.Desktop.Features.FakeServer.Providers.FakeServerDefinitionProvider().LoadJson(json));
        var published = new List<string>();
        using var subscription = backend.SubscribeAgent("harbor-shop", item => published.Add(item.Type));

        Assert.That(Run(backend, Post("/api/workspaces/harbor-shop/agent/messages", """{"message":"hi"}""")).Status, Is.EqualTo(204));
        Assert.That(published, Does.Contain("session_update"));
        Assert.That(backend.AgentEventsAfter("harbor-shop", 0).Select(item => item.Payload.ToJsonString()),
            Has.Some.Contain("Harbor Shop is running locally."));
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
