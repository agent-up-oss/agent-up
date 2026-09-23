using System.Diagnostics.CodeAnalysis;
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

    [Test]
    public void LoadJson_rejectsAMismatchedUrl()
    {
        var json = """{"id":"fake","url":"http://127.0.0.1:5000","displayName":"Demo","connection":{},"authentication":{},"entitlements":{},"workspaces":[]}""";

        Assert.That(
            () => new FakeServerDefinitionProvider().LoadJson(json),
            Throws.InvalidOperationException.With.Message.EqualTo("The fake server definition URL does not match the client catalog."));
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

    [Test]
    public async Task SendAsync_streamsAgentEventsAfterAPrompt()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, new RecordingHandler()))
        {
            BaseAddress = new Uri(FakeServerIdentity.Url)
        };

        using var promptBody = new StringContent("""{"message":"status?"}""", Encoding.UTF8, "application/json");
        using var prompt = await http.PostAsync(
            "/api/workspaces/harbor-shop/agent/messages",
            promptBody);
        using var response = await http.GetAsync(
            "/api/workspaces/harbor-shop/agent/events?after=0",
            HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[2048];
        var read = await stream.ReadAsync(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Does.Contain("user_message"));
        });
    }

    [Test]
    public async Task SendAsync_agentEventsWithoutAWorkspaceIdUseTheJsonHandler()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, new RecordingHandler()))
        {
            BaseAddress = new Uri(FakeServerIdentity.Url)
        };

        using var response = await http.GetAsync("/api/workspaces//agent/events");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task SendAsync_servesHtmlCharsetAndEmptyBodies()
    {
        var backend = FakeServerTestComposition.Backend();
        using var http = new HttpClient(new FakeServerMessageHandler(backend, new RecordingHandler()))
        {
            BaseAddress = new Uri(FakeServerIdentity.Url)
        };

        using var html = await http.GetAsync("/apps/harbor-shop/storefront");
        using var auditBody = new StringContent("{}");
        using var audit = await http.PostAsync("/api/audit/record", auditBody);
        var htmlBody = await html.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(html.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(htmlBody, Does.Contain("Harbor Mug"));
            Assert.That(audit.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
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

    [Test]
    public void UnsupportedMembers_throwOrNoOp()
    {
        using var stream = new FakeServerSseStream();

        Assert.Multiple(() =>
        {
            Assert.That(stream.CanRead, Is.True);
            Assert.That(stream.CanSeek, Is.False);
            Assert.That(stream.CanWrite, Is.False);
            Assert.That(() => stream.Length, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => stream.Position, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => stream.Position = 1, Throws.TypeOf<NotSupportedException>());
            Assert.That(() => stream.Seek(0, SeekOrigin.Begin), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => stream.SetLength(1), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => stream.Write(new byte[1], 0, 1), Throws.TypeOf<NotSupportedException>());
        });
        stream.Flush();
    }

    [Test]
    public async Task WriteFrame_afterCompleteIsIgnoredAndWaitUnblocks()
    {
        using var stream = new FakeServerSseStream();
        var buffer = new byte[64];
        var pending = stream.ReadAsync(buffer, 0, buffer.Length);
        stream.Complete();
        stream.Complete();
        stream.WriteFrame("data: ignored\n\n");

        Assert.That(await pending, Is.EqualTo(0));
    }

    [Test]
    public async Task ReadAsync_waitsForALaterFrame()
    {
        using var stream = new FakeServerSseStream();
        var buffer = new byte[64];
        var pending = stream.ReadAsync(buffer, 0, buffer.Length);
        stream.WriteFrame("data: later\n\n");
        stream.Complete();

        var read = await pending;
        Assert.That(Encoding.UTF8.GetString(buffer, 0, read), Is.EqualTo("data: later\n\n"));
    }

    [Test]
    public async Task ReadAsync_byteArrayOverloadCopiesAPartialFrame()
    {
        using var stream = new FakeServerSseStream();
        stream.WriteFrame("data: abcdefgh\n\n");
        stream.Complete();
        var first = new byte[6];
        var second = new byte[32];

        var firstRead = await stream.ReadAsync(first, 0, first.Length);
        var secondRead = await stream.ReadAsync(second, 0, second.Length);

        Assert.That(Encoding.UTF8.GetString(first, 0, firstRead), Is.EqualTo("data: "));
        Assert.That(Encoding.UTF8.GetString(second, 0, secondRead), Is.EqualTo("abcdefgh\n\n"));
        Assert.That(await stream.ReadAsync(second, 0, second.Length), Is.EqualTo(0));
    }
}

[TestFixture]
public sealed class FakeApplicationPageProviderTests
{
    [Test]
    public void Write_persistsHtmlUnderAFileUri()
    {
        var provider = new FakeApplicationPageProvider();
        var uri = provider.Write("harbor-shop", "storefront", "<html>Harbor Mug</html>");

        Assert.That(uri.IsFile, Is.True);
        Assert.That(File.ReadAllText(uri.LocalPath), Does.Contain("Harbor Mug"));
        File.Delete(uri.LocalPath);
    }

    [Test]
    public void Write_reusesTheSamePathForTheSameTab()
    {
        var provider = new FakeApplicationPageProvider();
        var first = provider.Write("harbor-shop", "storefront", "<html>one</html>");
        var second = provider.Write("harbor-shop", "storefront", "<html>two</html>");

        Assert.That(second, Is.EqualTo(first));
        Assert.That(File.ReadAllText(second.LocalPath), Does.Contain("two"));
        File.Delete(second.LocalPath);
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Respond());
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    private static HttpResponseMessage Respond() => new(HttpStatusCode.NoContent);
}
