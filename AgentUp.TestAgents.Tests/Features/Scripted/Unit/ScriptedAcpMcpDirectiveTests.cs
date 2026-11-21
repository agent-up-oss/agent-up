using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Unit;

/// <summary>A prompt that is an MCP directive makes the scripted agent call the tools it names.</summary>
[TestFixture]
public sealed class ScriptedAcpMcpDirectiveTests
{
    private const string Offered = """{"mcpServers":[{"type":"http","name":"agent-up-browser","url":"http://127.0.0.1:5001/mcp/browser","headers":[{"name":"X-Agent-Up-Agent-Session","value":"token-1"}]}]}""";

    [Test]
    public async Task RunAsync_advertisesHttpMcpOnlyWithAClient()
    {
        var withClient = await RunAsync(new RecordingClient(), Offered, prompt: null);
        var without = await RunAsync(client: null, Offered, prompt: null);

        Assert.Multiple(() =>
        {
            Assert.That(withClient, Does.Contain("\"mcpCapabilities\":{\"http\":true}"));
            Assert.That(without, Does.Contain("\"mcpCapabilities\":{\"http\":false}"));
        });
    }

    [Test]
    public async Task RunAsync_callsTheDirectedToolOnTheOfferedServerWithItsHeaders()
    {
        var client = new RecordingClient();

        var output = await RunAsync(client, Offered,
            """{"mcp":[{"server":"agent-up-browser","tool":"browser_click","arguments":{"selector":"#go"}}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls.Single().Server.Headers["X-Agent-Up-Agent-Session"], Is.EqualTo("token-1"));
            Assert.That(client.Calls.Single().Tool, Is.EqualTo("browser_click"));
            Assert.That(client.Calls.Single().Arguments["selector"]!.GetValue<string>(), Is.EqualTo("#go"));
            Assert.That(output, Does.Contain("\"status\":\"pending\"").And.Contain("\"status\":\"completed\""));
            Assert.That(output, Does.Contain("agent-up-browser_browser_click").And.Contain("end_turn"));
        });
    }

    [Test]
    public async Task RunAsync_failsACallToAServerItWasNotOfferedWithoutCallingIt()
    {
        var client = new RecordingClient();

        var output = await RunAsync(client, Offered,
            """{"mcp":[{"server":"agent-up-commits","tool":"enqueue_commit"}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty);
            Assert.That(output, Does.Contain("\"status\":\"failed\"").And.Contain("was not offered"));
            Assert.That(output, Does.Contain("1 failed"));
        });
    }

    [Test]
    public async Task RunAsync_treatsAnOrdinaryPromptAsAScriptedTurn()
    {
        var client = new RecordingClient();

        var output = await RunAsync(client, Offered, "hello");

        Assert.Multiple(() =>
        {
            Assert.That(client.Calls, Is.Empty);
            Assert.That(output, Does.Contain("end_turn"));
        });
    }

    private static async Task<string> RunAsync(IScriptedMcpClient? client, string sessionParameters, string? prompt)
    {
        var service = new ScriptedAcpService(new CompleteScript(), client);
        var lines = new List<string>
        {
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""",
            $$"""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{{sessionParameters}}}"""
        };
        if (prompt is not null)
            lines.Add(new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 3,
                ["method"] = "session/prompt",
                ["params"] = new JsonObject { ["prompt"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = prompt } } }
            }.ToJsonString());
        using var input = new StringReader(string.Join("\n", lines) + "\n");
        using var output = new StringWriter();
        await service.RunAsync(input, output, CancellationToken.None);
        return output.ToString();
    }

    private sealed class CompleteScript : IScriptedTurnScript
    {
        public ScriptedAgentScript Load() => new([new ScriptedTurnBuilder(ScriptedTurnKind.EndTurn).Build()]);
    }

    private sealed class RecordingClient : IScriptedMcpClient
    {
        public List<(ScriptedMcpServer Server, string Tool, JsonObject Arguments)> Calls { get; } = [];

        public Task<ScriptedMcpResult> CallAsync(ScriptedMcpServer server, string tool, JsonObject arguments, CancellationToken cancellationToken)
        {
            Calls.Add((server, tool, arguments));
            return Task.FromResult(new ScriptedMcpResult(false, "ok"));
        }
    }
}
