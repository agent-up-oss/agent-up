using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Providers;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Provider;

[TestFixture]
public sealed class ScriptedMcpProviderTests
{
    private static readonly ScriptedMcpServer Server = new(
        "agent-up-orchestration",
        "http://127.0.0.1:5001/mcp/orchestration",
        new Dictionary<string, string> { ["X-Agent-Up-Agent-Session"] = "token-1" });

    [TestCase("hello")]
    [TestCase("{ not json")]
    [TestCase("""{"other":[]}""")]
    public void Parse_ignoresAPromptThatIsNotADirective(string prompt)
    {
        Assert.That(ScriptedMcpDirectiveProvider.Parse(prompt), Is.Null);
    }

    [Test]
    public void Parse_readsEachCallAndDefaultsMissingArguments()
    {
        var calls = ScriptedMcpDirectiveProvider.Parse(
            """{"mcp":[{"server":"a","tool":"t1","arguments":{"x":1}},{"server":"b","tool":"t2"},{"tool":"no-server"}]}""")!;

        Assert.Multiple(() =>
        {
            Assert.That(calls.Select(call => call.Tool), Is.EqualTo(new[] { "t1", "t2" }));
            Assert.That(calls[0].Arguments["x"]!.GetValue<int>(), Is.EqualTo(1));
            Assert.That(calls[1].Arguments.Count, Is.Zero);
        });
    }

    [Test]
    public void Servers_readsTheOfferedHttpServersAndTheirHeaders()
    {
        var servers = ScriptedMcpDirectiveProvider.Servers(JsonNode.Parse(
            """{"mcpServers":[{"name":"agent-up-audit","url":"http://h/mcp/audit","headers":[{"name":"H","value":"v"}]},{"name":"broken"}]}"""));

        Assert.Multiple(() =>
        {
            Assert.That(servers.Single().Name, Is.EqualTo("agent-up-audit"));
            Assert.That(servers.Single().Headers["H"], Is.EqualTo("v"));
        });
    }

    [Test]
    public async Task CallAsync_initializesOnceSendsTheSessionHeadersAndReadsAnSseReply()
    {
        var handler = new McpHandler();
        var client = new HttpScriptedMcpClient(new HttpClient(handler));

        var first = await client.CallAsync(Server, "list_workspaces", new JsonObject(), CancellationToken.None);
        var second = await client.CallAsync(Server, "get_workspace_status", new JsonObject(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Methods, Is.EqualTo(new[] { "initialize", "notifications/initialized", "tools/call", "tools/call" }));
            Assert.That(handler.Headers, Has.All.EqualTo("token-1"));
            Assert.That(handler.Sessions.Skip(1), Has.All.EqualTo("mcp-session-1"));
            Assert.That(first, Is.EqualTo(new ScriptedMcpResult(false, "workspaces")));
            Assert.That(second.IsError, Is.False);
        });
    }

    [Test]
    public async Task CallAsync_reportsARefusedRequestAsAFailedCall()
    {
        var client = new HttpScriptedMcpClient(new HttpClient(new McpHandler(HttpStatusCode.NotFound)));

        var result = await client.CallAsync(Server, "start_workspace", new JsonObject(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.Text, Does.Contain("HTTP 404"));
        });
    }

    [Test]
    public void Result_readsAToolErrorAndAProtocolError()
    {
        var toolError = HttpScriptedMcpClient.Result(JsonNode.Parse(
            """{"result":{"isError":true,"content":[{"type":"text","text":"denied"}]}}"""));
        var protocolError = HttpScriptedMcpClient.Result(JsonNode.Parse("""{"error":{"code":-32602,"message":"Unknown tool"}}"""));

        Assert.Multiple(() =>
        {
            Assert.That(toolError, Is.EqualTo(new ScriptedMcpResult(true, "denied")));
            Assert.That(protocolError, Is.EqualTo(new ScriptedMcpResult(true, "Unknown tool")));
            Assert.That(HttpScriptedMcpClient.Body("event: message\n\n"), Is.Null);
        });
    }

    /// <summary>An MCP endpoint that answers initialize with a session id and tool calls as SSE.</summary>
    private sealed class McpHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public List<string?> Headers { get; } = [];
        public List<string?> Sessions { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            var method = body["method"]!.GetValue<string>();
            Methods.Add(method);
            Headers.Add(request.Headers.TryGetValues("X-Agent-Up-Agent-Session", out var token) ? token.Single() : null);
            Sessions.Add(request.Headers.TryGetValues("Mcp-Session-Id", out var session) ? session.Single() : null);
            if (status != HttpStatusCode.OK)
                return new HttpResponseMessage(status) { Content = new StringContent("") };
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (method == "initialize")
            {
                response.Headers.Add("Mcp-Session-Id", "mcp-session-1");
                response.Content = new StringContent("""{"jsonrpc":"2.0","id":1,"result":{}}""", Encoding.UTF8, "application/json");
                return response;
            }
            var reply = """{"jsonrpc":"2.0","id":2,"result":{"content":[{"type":"text","text":"workspaces"}]}}""";
            response.Content = new StringContent($"event: message\ndata: {reply}\n\n", Encoding.UTF8, "text/event-stream");
            return response;
        }
    }
}
