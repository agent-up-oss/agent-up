using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

/// <summary>
/// The Demo's forge connections. GitHub starts with a token, so the Tasks page can list the
/// example repository without asking for one.
/// </summary>
[TestFixture]
public sealed class FakeBackendTaskSourceTests
{
    [Test]
    public void TheDemoStartsWithGitHubConnected()
    {
        var backend = FakeServerTestComposition.Backend();

        var modules = Json(backend, Get("/api/task-source"))["modules"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(modules.Single(module => module!["moduleId"]!.GetValue<string>() == "github")!["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(modules.Single(module => module!["moduleId"]!.GetValue<string>() == "github")!["endpointUrl"]!.GetValue<string>(), Is.EqualTo("https://github.com"));
            Assert.That(modules.Single(module => module!["moduleId"]!.GetValue<string>() == "forgejo")!["connected"]!.GetValue<bool>(), Is.False);
        });
    }

    [Test]
    public void TheDemoListsTheExampleRepositoryAndItsIssues()
    {
        var backend = FakeServerTestComposition.Backend();

        var repositories = Json(backend, Get("/api/task-source/repositories")).AsArray();
        var issues = Json(backend, Get("/api/task-source/issues")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Single()!["name"]!.GetValue<string>(), Is.EqualTo("repository"));
            Assert.That(issues, Is.Not.Empty);
        });
    }

    [Test]
    public void ATokenThatCannotReadPullRequestsIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"moduleId":"forgejo","endpointUrl":"https://git.example.test","token":"denied"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void SavingATokenWithoutAHostUrlIsRefused()
    {
        var backend = FakeServerTestComposition.Backend();

        var refused = Run(backend, Put("/api/task-source", """{"moduleId":"forgejo","token":"forgejo-token"}"""));

        Assert.That(refused.Status, Is.EqualTo(409));
    }

    [Test]
    public void SavingATokenConnectsThatHost()
    {
        var backend = FakeServerTestComposition.Backend();

        var source = Json(backend, Put("/api/task-source", """{"moduleId":"forgejo","endpointUrl":"https://git.example.test","token":"forgejo-token"}"""));
        var forgejo = source["modules"]!.AsArray().Single(module => module!["moduleId"]!.GetValue<string>() == "forgejo")!;

        Assert.Multiple(() =>
        {
            Assert.That(forgejo["connected"]!.GetValue<bool>(), Is.True);
            Assert.That(forgejo["endpointUrl"]!.GetValue<string>(), Is.EqualTo("https://git.example.test"));
        });
    }

    [Test]
    public void ForgettingGitHubLeavesItsIssuesUnreadable()
    {
        var backend = FakeServerTestComposition.Backend();

        Json(backend, Delete("/api/task-source/github"));
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
