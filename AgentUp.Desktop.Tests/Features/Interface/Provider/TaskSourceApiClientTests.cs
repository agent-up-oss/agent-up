using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Interface.Providers;

namespace AgentUp.Desktop.Tests.Features.Interface.Provider;

[TestFixture]
public sealed class TaskSourceApiClientTests
{
    [Test]
    public async Task ReadAsync_readsWhereTheWorkComesFrom()
    {
        var handler = Returning("""
            {"moduleId":"github","owner":"agent-up-oss","repository":"agent-up","configured":true,
             "connected":true,"canConnect":true,"detail":"Reading issues.","modules":[]}
            """);

        var source = await new TaskSourceApiClient(Client(handler)).ReadAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Label, Is.EqualTo("agent-up-oss/agent-up"));
            Assert.That(source.Connected, Is.True);
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/task-source"));
        });
    }

    [Test]
    public async Task SetAsync_putsTheRepositoryTheReaderNamed()
    {
        var handler = Returning("""{"configured":true,"connected":false,"canConnect":true,"detail":"","modules":[]}""");

        await new TaskSourceApiClient(Client(handler))
            .SetAsync("github", "acme", "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("PUT api/task-source"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"owner\":\"acme\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"repository\":\"widgets\""));
        });
    }

    [Test]
    public async Task StartLoginAsync_answersWithSomewhereToSendTheReader()
    {
        var handler = Returning("""{"authorizeUrl":"https://example.test/authorize"}""");

        var url = await new TaskSourceApiClient(Client(handler)).StartLoginAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(url, Is.EqualTo("https://example.test/authorize"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("POST api/task-source/login"));
        });
    }

    [Test]
    public async Task AServerThatRefusesTheSignInAnswersWithNowhereRatherThanThrowing()
    {
        var client = new TaskSourceApiClient(Client(Returning("{}", HttpStatusCode.Conflict)));

        Assert.That(await client.StartLoginAsync(CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ClearAsync_takesTheSourceBackOut()
    {
        var handler = Returning("""{"configured":false,"connected":false,"canConnect":true,"detail":"","modules":[]}""");

        var source = await new TaskSourceApiClient(Client(handler)).ClearAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Configured, Is.False);
            Assert.That(handler.Requests.Single(), Is.EqualTo("DELETE api/task-source"));
        });
    }

    [Test]
    public void AServerThatRefusesAReadIsReportedRatherThanSwallowed()
    {
        var client = new TaskSourceApiClient(Client(Returning("", HttpStatusCode.Forbidden)));

        Assert.That(
            async () => await client.ReadAsync(CancellationToken.None),
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
