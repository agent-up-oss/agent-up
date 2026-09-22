using System.Net;
using System.Text;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Provider;

[TestFixture]
public sealed class FakeServerDefinitionProviderTests
{
    [Test]
    public void LoadEmbedded_matchesTheClientCatalogIdentity()
    {
        var definition = new FakeServerDefinitionProvider().LoadEmbedded();

        Assert.Multiple(() =>
        {
            Assert.That(definition.Id, Is.EqualTo(FakeServerIdentity.Id));
            Assert.That(definition.Url, Is.EqualTo(FakeServerIdentity.Url));
            Assert.That(definition.DisplayName, Is.EqualTo(FakeServerIdentity.DisplayName));
            Assert.That(definition.Workspaces, Is.Not.Empty);
        });
    }

    [Test]
    public void LoadJson_rejectsAMismatchedIdentity()
    {
        var json = """{"id":"other","url":"http://127.0.0.1:9","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[]}""";

        Assert.That(
            () => new FakeServerDefinitionProvider().LoadJson(json),
            Throws.InvalidOperationException.With.Message.EqualTo("The fake server definition id does not match the client catalog."));
    }
}

[TestFixture]
public sealed class FakeServerMessageHandlerTests
{
    [Test]
    public async Task SendAsync_servesTheFakeCatalogWithoutCallingTheInnerHandler()
    {
        var backend = FakeServerTestComposition.Backend();
        var inner = new RecordingHandler();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, inner))
        {
            BaseAddress = new Uri(FakeServerIdentity.Url)
        };

        var body = await http.GetStringAsync("/api/workspaces");

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("harbor-shop"));
            Assert.That(inner.Calls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SendAsync_forwardsNonFakeUrlsToTheInnerHandler()
    {
        var backend = FakeServerTestComposition.Backend();
        var inner = new RecordingHandler();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, inner))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };

        using var response = await http.GetAsync("/api/workspaces");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(inner.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SendAsync_streamsWorkspaceEvents()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, new RecordingHandler()))
        {
            BaseAddress = new Uri(FakeServerIdentity.Url)
        };

        using var response = await http.GetAsync("/api/workspaces/events", HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[1024];
        var read = await stream.ReadAsync(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Does.Contain("harbor-shop"));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Does.Contain("Healthy"));
        });
    }
}

[TestFixture]
public sealed class FakeServerSseStreamTests
{
    [Test]
    public async Task ReadAsync_returnsWrittenFramesThenCompletes()
    {
        using var stream = new FakeServerSseStream();
        stream.WriteFrame("data: one\n\n");
        stream.Complete();
        var buffer = new byte[64];

        var read = await stream.ReadAsync(buffer, 0, buffer.Length);

        Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Is.EqualTo("data: one\n\n"));
        Assert.That(await stream.ReadAsync(buffer, 0, buffer.Length), Is.EqualTo(0));
    }

    [Test]
    public void Read_isNotSupported()
    {
        using var stream = new FakeServerSseStream();

        Assert.That(() => stream.Read(new byte[8], 0, 8), Throws.TypeOf<NotSupportedException>());
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
}
