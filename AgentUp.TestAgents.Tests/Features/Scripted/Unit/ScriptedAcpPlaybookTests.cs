using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Scripted.Interfaces;
using AgentUp.TestAgents.Features.Scripted.Models;
using AgentUp.TestAgents.Features.Scripted.Services;

namespace AgentUp.TestAgents.Tests.Features.Scripted.Unit;

/// <summary>
/// A team of scripted agents answers prompts the Server writes, so each prompt is answered by the
/// first play whose phrases it contains: the files it writes, then the MCP calls it makes.
/// </summary>
[TestFixture]
public sealed class ScriptedAcpPlaybookTests
{
    private static readonly ScriptedPlay Fix = new(
        ["needs more work", "Keep it short."],
        [new ScriptedFileWrite("web/greeting.txt", "Ahoy\n")],
        [new ScriptedPlayCall("agent-up-tasks", "submit_work", new JsonObject { ["summary"] = "Shortened it." })]);

    private static readonly ScriptedPlay Start = new(
        ["Issue #"],
        [new ScriptedFileWrite("web/greeting.txt", "Ahoy there, sailor\n")],
        [new ScriptedPlayCall("agent-up-tasks", "submit_work", new JsonObject { ["summary"] = "Said Ahoy." })]);

    [Test]
    public async Task RunAsync_answersAPromptWithTheFirstPlayWhosePhrasesItContains()
    {
        var (files, mcp) = (new RecordingFiles(), new RecordingClient());

        var output = await RunAsync([Fix, Start], files, mcp, "Issue #1: Say Ahoy\n\nYour earlier change needs more work.\n- web/greeting.txt:1 (hector): Keep it short.");

        Assert.Multiple(() =>
        {
            Assert.That(files.Writes, Is.EqualTo(new[] { ("/work/bob", "web/greeting.txt", "Ahoy\n") }));
            Assert.That(mcp.Calls.Single().Tool, Is.EqualTo("submit_work"));
            Assert.That(mcp.Calls.Single().Arguments["summary"]!.GetValue<string>(), Is.EqualTo("Shortened it."));
            Assert.That(output, Does.Contain("\"title\":\"Write web/greeting.txt\"").And.Contain("\"status\":\"completed\""));
        });
    }

    [Test]
    public async Task RunAsync_fallsThroughToALaterPlayWhenAnEarlierOneDoesNotMatch()
    {
        var (files, mcp) = (new RecordingFiles(), new RecordingClient());

        await RunAsync([Fix, Start], files, mcp, "Issue #1: Say Ahoy\n\nIn web/greeting.txt replace Hello with Ahoy.");

        Assert.That(files.Writes.Single().Content, Is.EqualTo("Ahoy there, sailor\n"));
    }

    [Test]
    public async Task RunAsync_changesNothingForAPromptNoPlayAnswers()
    {
        var (files, mcp) = (new RecordingFiles(), new RecordingClient());

        var output = await RunAsync([Fix], files, mcp, "Review this change for issue #1");

        Assert.Multiple(() =>
        {
            Assert.That(files.Writes, Is.Empty);
            Assert.That(mcp.Calls, Is.Empty);
            Assert.That(output, Does.Contain("No play answers this prompt.").And.Contain("end_turn"));
        });
    }

    [Test]
    public async Task RunAsync_reportsAWriteTheWorkingDirectoryRefused()
    {
        var files = new RecordingFiles { Refusal = "Read-only file system" };

        var output = await RunAsync([Start], files, new RecordingClient(), "Issue #1: Say Ahoy");

        Assert.That(output, Does.Contain("\"status\":\"failed\""));
    }

    private static async Task<string> RunAsync(IReadOnlyList<ScriptedPlay> plays, IScriptedFileWriter files, IScriptedMcpClient mcp, string prompt)
    {
        var service = new ScriptedAcpService(new Playbook(plays), mcp, files);
        var lines = new[]
        {
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""",
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"/work/bob","mcpServers":[{"type":"http","name":"agent-up-tasks","url":"http://127.0.0.1:5001/mcp/tasks","headers":[]}]}}""",
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 3,
                ["method"] = "session/prompt",
                ["params"] = new JsonObject { ["prompt"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = prompt } } }
            }.ToJsonString()
        };
        using var input = new StringReader(string.Join("\n", lines) + "\n");
        using var output = new StringWriter();
        await service.RunAsync(input, output, CancellationToken.None);
        return output.ToString();
    }

    private sealed class Playbook(IReadOnlyList<ScriptedPlay> plays) : IScriptedTurnScript
    {
        public ScriptedAgentScript Load() => new([], plays);
    }

    private sealed class RecordingFiles : IScriptedFileWriter
    {
        public List<(string Root, string Path, string Content)> Writes { get; } = [];

        public string? Refusal { get; init; }

        public Task<string?> WriteAsync(string workingDirectory, string path, string content, CancellationToken cancellationToken)
        {
            if (Refusal is null)
                Writes.Add((workingDirectory, path, content));
            return Task.FromResult(Refusal);
        }
    }

    private sealed class RecordingClient : IScriptedMcpClient
    {
        public List<(string Tool, JsonObject Arguments)> Calls { get; } = [];

        public Task<ScriptedMcpResult> CallAsync(ScriptedMcpServer server, string tool, JsonObject arguments, CancellationToken cancellationToken)
        {
            Calls.Add((tool, arguments));
            return Task.FromResult(new ScriptedMcpResult(false, "ok"));
        }
    }
}
