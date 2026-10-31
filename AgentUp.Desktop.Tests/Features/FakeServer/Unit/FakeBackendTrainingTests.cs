using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Unit;

/// <summary>
/// The Demo's own decision backlog.
///
/// The Train surface reads the Demo over the same routes it reads a real Server over, so these
/// cover the shape the surface arranges rather than anything the Demo does for itself.
/// </summary>
[TestFixture]
public sealed class FakeBackendTrainingTests
{
    [Test]
    public void Repositories_countWhatIsWaitingPerRepository()
    {
        var backend = FakeServerTestComposition.Backend();

        var repositories = Json(backend, Get("/api/training/repositories")).AsArray();
        var example = repositories.Single(entry => entry!["repository"]!.GetValue<string>() == "repository");

        Assert.Multiple(() =>
        {
            Assert.That(repositories, Has.Count.EqualTo(2));
            Assert.That(example["owner"]!.GetValue<string>(), Is.EqualTo("example"));
            Assert.That(example["pending"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(example["reviewed"]!.GetValue<int>(), Is.EqualTo(2));
        });
    }

    [Test]
    public void Repositories_putTheOneWithTheMostWaitingFirst()
    {
        var backend = FakeServerTestComposition.Backend();

        var repositories = Json(backend, Get("/api/training/repositories")).AsArray();

        Assert.That(repositories[0]!["repository"]!.GetValue<string>(), Is.EqualTo("repository"));
    }

    [Test]
    public void Repositories_nameTheThreeCategoriesTheSurfaceShows()
    {
        var backend = FakeServerTestComposition.Backend();

        var categories = Json(backend, Get("/api/training/repositories"))[0]!["categories"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                categories.Select(category => category!["category"]!.GetValue<string>()),
                Is.EqualTo(new[] { "product-vision", "gui-decisions", "code-architecture" }));
            Assert.That(
                categories.Select(category => category!["label"]!.GetValue<string>()),
                Is.EqualTo(new[] { "Product vision", "GUI", "Architecture" }));
        });
    }

    [Test]
    public void Decisions_answerOneRepositoryAndPutWhatIsWaitingFirst()
    {
        var backend = FakeServerTestComposition.Backend();

        var decisions = Json(backend, Get("/api/training/decisions")
            .WithQuery("?owner=example&repository=repository")).AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(decisions, Has.Count.EqualTo(5));
            Assert.That(decisions[0]!["verdict"]!.GetValue<string>(), Is.EqualTo("pending"));
            Assert.That(decisions[^1]!["verdict"]!.GetValue<string>(), Is.Not.EqualTo("pending"));
        });
    }

    [Test]
    public void Decisions_narrowToOneCategoryWhereOneIsAskedFor()
    {
        var backend = FakeServerTestComposition.Backend();

        var decisions = Json(backend, Get("/api/training/decisions")
            .WithQuery("?owner=example&repository=repository&category=product-vision")).AsArray();

        Assert.That(
            decisions.Select(decision => decision!["id"]!.GetValue<string>()),
            Is.EquivalentTo(new[] { "pd-3", "pd-4" }));
    }

    [Test]
    public void Review_keepsTheVerdictForTheRestOfTheSession()
    {
        var backend = FakeServerTestComposition.Backend();

        var answered = Json(backend, Post("/api/training/decisions/pd-1/review",
            """{"verdict":"good","note":"  reads well  "}"""));
        var reread = Json(backend, Get("/api/training/decisions").WithQuery("?owner=example&repository=repository"))
            .AsArray()
            .Single(decision => decision!["id"]!.GetValue<string>() == "pd-1");

        Assert.Multiple(() =>
        {
            Assert.That(answered["verdict"]!.GetValue<string>(), Is.EqualTo("good"));
            Assert.That(answered["note"]!.GetValue<string>(), Is.EqualTo("reads well"));
            Assert.That(answered["reviewedAt"], Is.Not.Null);
            Assert.That(reread["verdict"]!.GetValue<string>(), Is.EqualTo("good"));
        });
    }

    [Test]
    public void Review_refusesAVerdictNobodyCanGive()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.Multiple(() =>
        {
            Assert.That(Run(backend, Post("/api/training/decisions/pd-1/review", """{"verdict":"maybe"}""")).Status,
                Is.EqualTo(404));
            Assert.That(Run(backend, Post("/api/training/decisions/nope/review", """{"verdict":"good"}""")).Status,
                Is.EqualTo(404));
        });
    }

    [Test]
    public void Evidence_handsOverThePictureBehindADecision()
    {
        var backend = FakeServerTestComposition.Backend();

        var image = Json(backend, Get("/api/training/evidence/ev-1"));

        Assert.Multiple(() =>
        {
            Assert.That(image["evidenceId"]!.GetValue<string>(), Is.EqualTo("ev-1"));
            Assert.That(image["mimeType"]!.GetValue<string>(), Is.EqualTo("image/png"));
            Assert.That(image["base64"]!.GetValue<string>(), Is.Not.Empty);
        });
    }

    [Test]
    public void Evidence_saysNotFoundForAPictureTheBacklogDoesNotHold()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Get("/api/training/evidence/ev-missing")).Status, Is.EqualTo(404));
    }

    [Test]
    public void Export_carriesOnlyTheJudgedDecisions()
    {
        var backend = FakeServerTestComposition.Backend();

        var response = Run(backend, Get("/api/training/export"));
        var lines = Lines(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.ContentType, Is.EqualTo("application/x-ndjson"));
            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines.Select(line => JsonNode.Parse(line)!["verdict"]!.GetValue<string>()),
                Has.None.EqualTo("pending"));
        });
    }

    [Test]
    public void Export_includesWhatIsWaitingWhereItIsAskedFor()
    {
        var backend = FakeServerTestComposition.Backend();

        var lines = Lines(Run(backend, Get("/api/training/export").WithQuery("?includePending=true")));

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(6));
            Assert.That(JsonNode.Parse(lines[0])!["repository"]!.GetValue<string>(), Is.EqualTo("example/repository"));
        });
    }

    [Test]
    public void Training_saysNotFoundForARouteTheSurfaceNeverAsksFor()
    {
        var backend = FakeServerTestComposition.Backend();

        Assert.That(Run(backend, Get("/api/training/nonsense")).Status, Is.EqualTo(404));
    }

    private static string[] Lines(FakeBackendResponseDto response)
        => (response.Body ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static FakeBackendRequestDtoBuilder Get(string path) => new FakeBackendRequestDtoBuilder().Get(path);

    private static FakeBackendRequestDtoBuilder Post(string path, string? body = null)
        => new FakeBackendRequestDtoBuilder().Post(path, body);

    private static FakeBackendResponseDto Run(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => backend.Handle(request.Build());

    private static JsonNode Json(FakeBackendService backend, FakeBackendRequestDtoBuilder request)
        => JsonNode.Parse(Run(backend, request).Body!)!;
}
