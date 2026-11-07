using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class OllamaChatModelProviderTests
{
    [Test]
    public async Task ChatAsync_postsToolsAndReadsToolCalls()
    {
        var handler = new RecordingHandler("""
            {"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"browser_screenshot","arguments":{"fullPage":true}}}]}}
            """);
        var provider = Provider(handler, ("AGENTUP_OLLAMA_MODEL", "qwen3-vl"), ("AGENTUP_OLLAMA_URL", "http://ollama.test:11434"));

        var reply = await provider.ChatAsync(
            [new InProcessChatMessage("user", "look at the page")],
            [new InProcessTool("browser_screenshot", "Screenshot.", JsonSerializer.SerializeToElement(new { type = "object" }))],
            CancellationToken.None);

        using var sent = JsonDocument.Parse(handler.Body!);
        Assert.Multiple(() =>
        {
            Assert.That(handler.Uri, Is.EqualTo(new Uri("http://ollama.test:11434/api/chat")));
            Assert.That(sent.RootElement.GetProperty("model").GetString(), Is.EqualTo("qwen3-vl"));
            Assert.That(sent.RootElement.GetProperty("stream").GetBoolean(), Is.False);
            Assert.That(sent.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString(), Is.EqualTo("browser_screenshot"));
            Assert.That(reply.ToolCalls![0].Name, Is.EqualTo("browser_screenshot"));
            Assert.That(reply.ToolCalls[0].Arguments.GetProperty("fullPage").GetBoolean(), Is.True);
        });
    }

    [Test]
    public void Parse_acceptsOpenAiStyleStringArguments()
    {
        var reply = OllamaChatModelProvider.Parse(JsonSerializer.Deserialize<JsonElement>("""
            {"message":{"content":"ok","tool_calls":[{"function":{"name":"browser_click","arguments":"{\"selector\":\"#go\"}"}}]}}
            """));

        Assert.That(reply.ToolCalls![0].Arguments.GetProperty("selector").GetString(), Is.EqualTo("#go"));
    }

    [Test]
    public void Request_sendsScreenshotsAsImagesOnTheToolMessage()
    {
        var body = OllamaChatModelProvider.Request(
            "model",
            [new InProcessChatMessage("tool", "captured", ToolName: "browser_screenshot", Images: ["aGVsbG8="])],
            []);

        var message = body["messages"]![0]!;
        Assert.Multiple(() =>
        {
            Assert.That(message["tool_name"]!.GetValue<string>(), Is.EqualTo("browser_screenshot"));
            Assert.That(message["images"]![0]!.GetValue<string>(), Is.EqualTo("aGVsbG8="));
            Assert.That(body.ContainsKey("tools"), Is.False);
        });
    }

    [Test]
    public void Configured_requiresAModel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Provider(new RecordingHandler("{}")).Configured, Is.False);
            Assert.That(Provider(new RecordingHandler("{}"), ("AGENTUP_OLLAMA_MODEL", "m")).Settings()!.Endpoint,
                Is.EqualTo(new Uri("http://127.0.0.1:11434/")));
        });
    }

    [Test]
    public void ChatAsync_reportsAFailingStatusAsAnAgentError()
    {
        var provider = Provider(new RecordingHandler("model not found", HttpStatusCode.NotFound), ("AGENTUP_OLLAMA_MODEL", "missing"));

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ChatAsync([new InProcessChatMessage("user", "hi")], [], CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("404").And.Contain("model not found"));
    }

    private static OllamaChatModelProvider Provider(RecordingHandler handler, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();
        return new OllamaChatModelProvider(new SingleClientFactory(handler), configuration);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
