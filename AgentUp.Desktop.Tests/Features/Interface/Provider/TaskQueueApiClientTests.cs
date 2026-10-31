using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Providers;

namespace AgentUp.Desktop.Tests.Features.Interface.Provider;

[TestFixture]
public sealed class TaskQueueApiClientTests
{
    [Test]
    public async Task ListAsync_readsTheQueueTheServerHolds()
    {
        var handler = Returning("""
            [{"id":"I-44","owner":"example","repository":"repository","number":44,"title":"Restock","url":"u","targetWorkspaceId":null,"heldByWorkspaceId":"bob"}]
            """);

        var queue = await new TaskQueueApiClient(Client(handler)).ListAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(queue.Single().Number, Is.EqualTo(44));
            Assert.That(queue.Single().HeldByWorkspaceId, Is.EqualTo("bob"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/tasks"));
        });
    }

    [Test]
    public async Task ListAsync_readsAnEmptyQueueAsEmptyRatherThanNull()
    {
        var queue = await new TaskQueueApiClient(Client(Returning("[]"))).ListAsync(CancellationToken.None);

        Assert.That(queue, Is.Empty);
    }

    [Test]
    public async Task ListIssuesAsync_asksTheWorkspacesOwnForgeRoute()
    {
        var handler = Returning("""[{"id":"I-57","number":57,"title":"Cart badge","url":"u"}]""");

        var issues = await new TaskQueueApiClient(Client(handler)).ListIssuesAsync("ws bob", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(issues.Single().Number, Is.EqualTo(57));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/workspaces/ws%20bob/forge/issues"));
        });
    }

    [Test]
    public async Task EnqueueAsync_postsTheWorkspaceTheIssueAndTheTarget()
    {
        var handler = Returning("{}");

        await new TaskQueueApiClient(Client(handler)).EnqueueAsync("bob", "I-57", "hector", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("POST api/tasks"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"issueId\":\"I-57\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"targetWorkspaceId\":\"hector\""));
        });
    }

    [Test]
    public async Task RemoveAsync_addressesTheTaskByOwnerRepositoryAndId()
    {
        var handler = Returning("", HttpStatusCode.NoContent);

        await new TaskQueueApiClient(Client(handler)).RemoveAsync(Task("I-44"), CancellationToken.None);

        Assert.That(handler.Requests.Single(), Is.EqualTo("DELETE api/tasks/example/repository/I-44"));
    }

    [Test]
    public async Task MoveAsync_putsTheNewPosition()
    {
        var handler = Returning("", HttpStatusCode.NoContent);

        await new TaskQueueApiClient(Client(handler)).MoveAsync(Task("I-44"), 2, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("PUT api/tasks/example/repository/I-44/position"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"index\":2"));
        });
    }

    [Test]
    public async Task TargetAsync_putsTheWorkspaceToPointTheTaskAt()
    {
        var handler = Returning("", HttpStatusCode.NoContent);

        await new TaskQueueApiClient(Client(handler)).TargetAsync(Task("I-44"), "hector", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("PUT api/tasks/example/repository/I-44/target"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"workspaceId\":\"hector\""));
        });
    }

    [Test]
    public async Task ReleaseAsync_postsToTheReleaseRoute()
    {
        var handler = Returning("", HttpStatusCode.NoContent);

        await new TaskQueueApiClient(Client(handler)).ReleaseAsync(Task("I-44"), CancellationToken.None);

        Assert.That(handler.Requests.Single(), Is.EqualTo("POST api/tasks/example/repository/I-44/release"));
    }

    [Test]
    public void AServerThatRefusesTheChangeIsReportedRatherThanSwallowed()
    {
        var client = new TaskQueueApiClient(Client(Returning("", HttpStatusCode.Conflict)));

        Assert.That(
            async () => await client.RemoveAsync(Task("I-44"), CancellationToken.None),
            Throws.InstanceOf<HttpRequestException>());
    }

    private static QueuedTask Task(string id) =>
        new(id, "example", "repository", 44, "Restock", "u", null, null);

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
