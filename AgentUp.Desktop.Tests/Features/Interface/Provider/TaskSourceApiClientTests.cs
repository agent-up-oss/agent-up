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
            {"identities":[{"workspaceId":"bob","displayName":"Bob","initials":"BO","moduleId":"github","connected":true,"detail":"Reading repositories.","endpointUrl":"https://github.com"}]}
            """);

        var source = await new TaskSourceApiClient(Client(handler)).ReadAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Identities.Single().Connected, Is.True);
            Assert.That(source.Identities.Single().EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(source.Identities.Single().Initials, Is.EqualTo("BO"));
            Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/task-source"));
        });
    }

    [Test]
    public async Task SaveAsync_putsTheTokenAndDoesNotKeepItInTheAnswer()
    {
        var handler = Returning("""{"identities":[{"workspaceId":"bob","displayName":"Bob","initials":"BO","moduleId":"github","connected":true,"detail":"Reading."}]}""");

        var source = await new TaskSourceApiClient(Client(handler)).SaveAsync("bob", "github", "https://github.com", "secret-token", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests.Single(), Is.EqualTo("PUT api/task-source"));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"workspaceId\":\"bob\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"token\":\"secret-token\""));
            Assert.That(handler.Bodies.Single(), Does.Contain("\"endpointUrl\":\"https://github.com\""));
            Assert.That(source!.Identities.Single().Detail, Does.Not.Contain("secret-token"));
        });
    }

    [Test]
    public async Task SaveAsync_surfacesTheServersReasonWhenTheTokenIsRefused()
    {
        var client = new TaskSourceApiClient(Client(Returning(
            """{"title":"Task source","detail":"The token cannot read pull requests."}""",
            HttpStatusCode.Conflict)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SaveAsync("bob", "github", "https://github.com", "narrow", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("pull requests"));
    }

    [Test]
    public async Task ForgetAsync_forgetsOneHost()
    {
        var handler = Returning("""{"identities":[{"workspaceId":"bob","displayName":"Bob","initials":"BO","connected":false,"detail":""}]}""");

        var source = await new TaskSourceApiClient(Client(handler)).ForgetAsync("bob", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Identities.Single().Connected, Is.False);
            Assert.That(handler.Requests.Single(), Is.EqualTo("DELETE api/task-source/bob"));
        });
    }

    [Test]
    public async Task ListRepositoriesAsync_readsTheRepositoriesTheTokenCanSee()
    {
        var handler = Returning("""[{"moduleId":"github","owner":"example","name":"repository"}]""");

        var repositories = await new TaskSourceApiClient(Client(handler)).ListRepositoriesAsync(CancellationToken.None);

        Assert.That(repositories.Single().Name, Is.EqualTo("repository"));
        Assert.That(handler.Requests.Single(), Is.EqualTo("GET api/task-source/repositories"));
    }

    [Test]
    public async Task ListRepositoriesAsync_readsAMissingListAsEmpty()
    {
        var repositories = await new TaskSourceApiClient(Client(Returning("null"))).ListRepositoriesAsync(CancellationToken.None);

        Assert.That(repositories, Is.Empty);
    }

    [Test]
    public async Task ListPullsAsync_readsAMissingListAsEmpty()
    {
        var pulls = await new TaskSourceApiClient(Client(Returning("null")))
            .ListPullsAsync("github", "example", "repository", CancellationToken.None);

        Assert.That(pulls, Is.Empty);
    }

    [Test]
    public async Task ListPullsAsync_asksForTheNamedRepository()
    {
        var handler = Returning("""[{"id":"42","number":42,"title":"Product cards","url":"u","reviews":[]}]""");

        var pulls = await new TaskSourceApiClient(Client(handler))
            .ListPullsAsync("github", "example", "repository", CancellationToken.None);

        Assert.That(pulls.Single().Number, Is.EqualTo(42));
        Assert.That(handler.Requests.Single(), Does.Contain("api/task-source/pulls").And.Contain("moduleId=github"));
    }

    [Test]
    public async Task SaveAsync_surfacesAHostThatCannotBeReached()
    {
        var client = new TaskSourceApiClient(Client(Returning(
            """{"title":"Task source","detail":"Could not reach GitHub to check the token."}""",
            HttpStatusCode.ServiceUnavailable)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SaveAsync("bob", "github", "https://github.com", "token", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("Could not reach GitHub"));
    }

    [Test]
    public async Task SaveAsync_namesTheStatusWhenTheRefusalJsonHasNoDetail()
    {
        var client = new TaskSourceApiClient(Client(Returning("""{"title":"Task source"}""", HttpStatusCode.Conflict)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SaveAsync("bob", "github", "https://github.com", "token", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("409"));
    }

    [Test]
    public async Task SaveAsync_namesTheStatusWhenTheRefusalHasNoDetail()
    {
        var client = new TaskSourceApiClient(Client(Returning("not-json", HttpStatusCode.Conflict)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SaveAsync("bob", "github", "https://github.com", "token", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("409"));
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
