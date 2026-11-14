using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Interface.Providers;

namespace AgentUp.Desktop.Tests.Features.Interface.Provider;

[TestFixture]
public sealed class TrainingApiClientTests
{
    [Test]
    public async Task ListRepositoriesAsync_readsTheTilesTheServerCounts()
    {
        var handler = Returning("""
            [{"owner":"example","repository":"repository","pending":2,"reviewed":1,
              "categories":[{"category":"product-vision","label":"Product vision","pending":2,"reviewed":1}]}]
            """);

        var repositories = await new TrainingApiClient(Client(handler)).ListRepositoriesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Single().Key, Is.EqualTo("example/repository"));
            Assert.That(repositories.Single().Pending, Is.EqualTo(2));
            Assert.That(repositories.Single().Categories.Single().Label, Is.EqualTo("Product vision"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/training/repositories"));
        });
    }

    [Test]
    public async Task ListDecisionsAsync_namesTheRepositoryItIsAskingAbout()
    {
        var handler = Returning("""
            [{"id":"pd-1","owner":"example","repository":"repo it","workspaceId":"ws","agent":"Lucy",
              "category":"gui-decisions","headline":"H","detail":"D","evidence":"E","evidenceImages":[],
              "recordedAt":"2026-01-01T00:00:00Z","verdict":"pending","note":null,"reviewedAt":null}]
            """);

        var decisions = await new TrainingApiClient(Client(handler))
            .ListDecisionsAsync("example", "repo it", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decisions.Single().Id, Is.EqualTo("pd-1"));
            Assert.That(decisions.Single().Reviewed, Is.False);
            Assert.That(handler.Requests.Single(),
                Is.EqualTo("GET api/training/decisions?owner=example&repository=repo%20it"));
        });
    }

    [Test]
    public async Task EvidenceAsync_readsThePictureBytesRatherThanAUrlTheViewWouldFetch()
    {
        var handler = Returning("""{"evidenceId":"ev-1","mimeType":"image/png","base64":"AAAB"}""");

        var image = await new TrainingApiClient(Client(handler)).EvidenceAsync("ev 1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(image!.Base64, Is.EqualTo("AAAB"));
            Assert.That(image.MimeType, Is.EqualTo("image/png"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/training/evidence/ev%201"));
        });
    }

    [Test]
    public async Task EvidenceAsync_answersNullWhereTheBacklogNoLongerHoldsIt()
    {
        var client = new TrainingApiClient(Client(Returning("", HttpStatusCode.NotFound)));

        Assert.That(await client.EvidenceAsync("ev-1", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ReviewAsync_postsTheVerdictAndTheNote()
    {
        var handler = Returning("{}");

        await new TrainingApiClient(Client(handler)).ReviewAsync("pd 1", "good", "reads well", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("POST api/training/decisions/pd%201/review"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"verdict\":\"good\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"note\":\"reads well\""));
        });
    }

    [Test]
    public async Task ExportAsync_asksForTheJudgedBacklogAsItStands()
    {
        var handler = Returning("{\"id\":\"pd-2\"}\n");

        var jsonl = await new TrainingApiClient(Client(handler)).ExportAsync(false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(jsonl, Does.Contain("pd-2"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/training/export?includePending=false"));
        });
    }

    [Test]
    public void AServerThatRefusesAReadIsReportedRatherThanSwallowed()
    {
        var client = new TrainingApiClient(Client(Returning("", HttpStatusCode.Forbidden)));

        Assert.That(
            async () => await client.ListRepositoriesAsync(CancellationToken.None),
            Throws.InstanceOf<HttpRequestException>());
    }

    private static HttpClient Client(RecordingHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost:5000/") };

    private static RecordingHandler Returning(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(body, status);

    private sealed class RecordingHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method.Method} {request.RequestUri!.PathAndQuery.TrimStart('/')}");
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
