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
            {"modules":[{"moduleId":"github","displayName":"GitHub","connected":true,"detail":"Reading repositories.","endpointUrl":"https://github.com"}]}
            """);

        var source = await new TaskSourceApiClient(Client(handler)).ReadAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Modules.Single().Connected, Is.True);
            Assert.That(source.Modules.Single().EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/task-source"));
        });
    }

    [Test]
    public async Task SaveAsync_putsTheTokenAndDoesNotKeepItInTheAnswer()
    {
        var handler = Returning("""{"modules":[{"moduleId":"github","displayName":"GitHub","connected":true,"detail":"Reading."}]}""");

        var source = await new TaskSourceApiClient(Client(handler)).SaveAsync("github", "https://github.com", "secret-token", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("PUT api/task-source"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"token\":\"secret-token\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"endpointUrl\":\"https://github.com\""));
            Assert.That(source!.Modules.Single().Detail, Does.Not.Contain("secret-token"));
        });
    }

    [Test]
    public async Task SaveAsync_surfacesTheServersReasonWhenTheTokenIsRefused()
    {
        var client = new TaskSourceApiClient(Client(Returning(
            """{"title":"Task source","detail":"The token cannot read pull requests."}""",
            HttpStatusCode.Conflict)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveAsync("github", "https://github.com", "narrow", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("pull requests"));
    }

    [Test]
    public async Task ForgetAsync_forgetsOneHost()
    {
        var handler = Returning("""{"modules":[{"moduleId":"github","displayName":"GitHub","connected":false,"detail":""}]}""");

        var source = await new TaskSourceApiClient(Client(handler)).ForgetAsync("github", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Modules.Single().Connected, Is.False);
            Assert.That(handler.Requests.Single(), Is.EqualTo("DELETE api/task-source/github"));
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
