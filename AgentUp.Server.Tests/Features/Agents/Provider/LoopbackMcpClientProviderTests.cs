using System.Net;
using System.Text;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LoopbackMcpClientProviderTests
{
    [Test]
    public void BaseUrl_dialsLoopbackForAWildcardListenerAndPrefersHttp()
    {
        var provider = Provider(new ScriptedMcpHandler(), ("urls", "https://*:5443;http://0.0.0.0:5001"));

        Assert.That(provider.BaseUrl(), Is.EqualTo(new Uri("http://127.0.0.1:5001/")));
    }

    [Test]
    public void Parse_readsTheDataFrameOfAnEventStreamAnswer()
    {
        var message = LoopbackMcpClientProvider.Parse("event: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{\"tools\":[]}}\n\n");

        Assert.That(message.GetProperty("id").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task ListToolsAsync_initializesOnceAndReusesTheSession()
    {
        var handler = new ScriptedMcpHandler();
        var provider = Provider(handler, ("AGENTUP_INPROCESS_MCP_URL", "http://127.0.0.1:5001"));

        var first = await provider.ListToolsAsync("/mcp/browser", CancellationToken.None);
        await provider.ListToolsAsync("/mcp/browser", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Single().GetProperty("name").GetString(), Is.EqualTo("browser_click"));
            Assert.That(handler.Methods, Is.EqualTo(new[] { "initialize", "notifications/initialized", "tools/list", "tools/list" }));
            Assert.That(handler.Sessions.Skip(1), Is.All.EqualTo("session-1"));
        });
    }

    private static LoopbackMcpClientProvider Provider(HttpMessageHandler handler, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();
        return new LoopbackMcpClientProvider(new SingleClientFactory(handler), configuration);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ScriptedMcpHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public List<string?> Sessions { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = System.Text.Json.JsonDocument.Parse(body);
            var method = document.RootElement.GetProperty("method").GetString()!;
            Methods.Add(method);
            Sessions.Add(request.Headers.TryGetValues("Mcp-Session-Id", out var values) ? values.Single() : null);
            var response = new HttpResponseMessage(method == "notifications/initialized" ? HttpStatusCode.Accepted : HttpStatusCode.OK)
            {
                Content = new StringContent(method switch
                {
                    "initialize" => """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18"}}""",
                    "tools/list" => """{"jsonrpc":"2.0","id":2,"result":{"tools":[{"name":"browser_click","inputSchema":{"type":"object"}}]}}""",
                    _ => ""
                }, Encoding.UTF8, "application/json")
            };
            if (method == "initialize")
                response.Headers.Add("Mcp-Session-Id", "session-1");
            return response;
        }
    }
}
