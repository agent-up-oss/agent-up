using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

/// <summary>
/// The Demo's per-member forge connections. Bob starts with a GitHub token, so the Tasks page
/// can list the example repository without asking for one.
/// </summary>
[TestFixture]
public sealed class FakeBackendTaskSourceTests
{
    [Test]
    public void TheDemoStartsWithBobsGitHubConnected()
    {
        var backend = FakeServerTestComposition.Backend();

        var identities = Json(backend, Get("/api/task-source"))["identities"]!.AsArray();
        var bob = identities.Single(identity => identity!["workspaceId"]!.GetValue<string>() == "bob")!;

        Assert.Multiple(() =>
        {
            Assert.That(bob["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(bob["moduleId"]!.GetValue<string>(), Is.EqualTo("github"));
            Assert.That(bob["initials"]!.GetValue<string>(), Is.EqualTo("BO"));
            Assert.That(bob["endpointUrl"]!.GetValue<string>(), Is.EqualTo("https://github.com"));
            Assert.That(identities.Single(identity => identity!["workspaceId"]!.GetValue<string>() == "frank")!["connected"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public void TheDemoListsTheExampleRepositoryAndTheMembersWhoCanSeeIt()
    {
        var backend = FakeServerTestComposition.Backend();

        var repositories = Json(backend, Get("/api/task-source/repositories")).AsArray();
        var issues = Json(backend, Get("/api/task-source/issues")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Single()!["name"]!.GetValue<string>(), Is.EqualTo("repository"));
            Assert.That(repositories.Single()!["members"]!.AsArray().Single()!["initials"]!.GetValue<string>(), Is.EqualTo("BO"));
            Assert.That(issues, Is.Not.Empty);
        });
    }

    [Test]
    public void ATokenThatCannotReadPullRequestsIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","endpointUrl":"https://git.example.test","token":"denied"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void SavingATokenWithoutAHostUrlIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","token":"forgejo-token"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void SavingAGitHubTokenKeepsThatMemberConnected()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Delete("/api/task-source/bob"));

        var source = Json(backend, Put("/api/task-source", """{"workspaceId":"bob","moduleId":"github","endpointUrl":"https://github.com","token":"github-token"}"""));
        var bob = source["identities"]!.AsArray().Single(identity => identity!["workspaceId"]!.GetValue<string>() == "bob")!;

        Assert.That(bob["connected"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void PullsAreEmptyOnceEveryMemberIsForgotten()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Delete("/api/task-source/bob"));

        Assert.That(Json(backend, Get("/api/task-source/pulls")).AsArray(), Is.Empty);
    }

    [Test]
    public void SavingATokenConnectsThatMember()
    {
        var backend = FakeServerTestComposition.Backend();

        var source = Json(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","endpointUrl":"https://git.example.test","token":"forgejo-token"}"""));
        var frank = source["identities"]!.AsArray().Single(identity => identity!["workspaceId"]!.GetValue<string>() == "frank")!;
        var repositories = Json(backend, Get("/api/task-source/repositories")).AsArray();
        var forgejo = repositories.Single(row => row!["moduleId"]!.GetValue<string>() == "forgejo")!;

        Assert.Multiple(() =>
        {
            Assert.That(frank["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(frank["endpointUrl"]!.GetValue<string>(), Is.EqualTo("https://git.example.test"));
            Assert.That(forgejo["members"]!.AsArray().Single()!["displayName"]!.GetValue<string>(), Is.EqualTo("Frank"));
        });
    }

    [Test]
    public void ForgettingBobLeavesHisIssuesUnreadable()
    {
        var backend = FakeServerTestComposition.Backend();

        Json(backend, Delete("/api/task-source/bob"));
        var issues = Json(backend, Get("/api/task-source/issues")).AsArray();

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public void ForgettingEveryHostLeavesNothingToPick()
    {
        var backend = FakeServerTestComposition.Backend();

        Json(backend, Delete("/api/task-source"));

        Assert.That(Run(backend, Post("/api/tasks", """{"issueId":"I-57"}""")).Status, Is.EqualTo(404));
    }

    [Test]
    public void SavingATokenOnAnUnknownHostIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"workspaceId":"bob","moduleId":"gitlab","endpointUrl":"https://gitlab.com","token":"token"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void SavingAnEmptyTokenIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","endpointUrl":"https://git.example.test","token":" "}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void ANonHttpHostUrlIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","endpointUrl":"git.example.test","token":"token"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void ForgettingFrankLeavesBobConnected()
    {
        var backend = FakeServerTestComposition.Backend();
        Json(backend, Put("/api/task-source", """{"workspaceId":"frank","moduleId":"forgejo","endpointUrl":"https://git.example.test","token":"forgejo-token"}"""));

        Json(backend, Delete("/api/task-source/frank"));
        var source = Json(backend, Get("/api/task-source"));

        Assert.Multiple(() =>
        {
            Assert.That(source["identities"]!.AsArray().Single(identity => identity!["workspaceId"]!.GetValue<string>() == "bob")!["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(source["identities"]!.AsArray().Single(identity => identity!["workspaceId"]!.GetValue<string>() == "frank")!["connected"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public void AnUnknownTaskSourceRouteIsNotFound()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Get("/api/task-source/unknown")).Status, Is.EqualTo(404));
    }

    [Test]
    public void TheOpenPullRequestCarriesTheReviewItStillNeeds()
    {
        var backend = FakeServerTestComposition.Backend();

        var review = Json(backend, Get("/api/task-source/pulls")).AsArray().Single()!["reviews"]!.AsArray().Single()!;

        Assert.Multiple(() =>
        {
            Assert.That(review["kind"]!.GetValue<string>(), Is.EqualTo("devReviewer"));
            Assert.That(review["standing"]!.GetValue<string>(), Is.EqualTo("reviewing"));
        });
    }

    private static FakeBackendRequestDtoBuilder Get(string path) => new FakeBackendRequestDtoBuilder().Get(path);

    private static FakeBackendRequestDtoBuilder Put(string path, string body)
        => new FakeBackendRequestDtoBuilder().Put(path, body);

    private static FakeBackendRequestDtoBuilder Post(string path, string body)
        => new FakeBackendRequestDtoBuilder().Post(path, body);

    private static FakeBackendRequestDtoBuilder Delete(string path)
        => new FakeBackendRequestDtoBuilder().Delete(path);

    private static FakeBackendResponseDto Run(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => backend.Handle(request.Build());

    private static JsonNode Json(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => JsonNode.Parse(Run(backend, request).Body!)!;
}
