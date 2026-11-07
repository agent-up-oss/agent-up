using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class InProcessAgentProcessProviderTests
{
    [Test]
    public async Task Initialize_advertisesAcpV1WithNoSignIn()
    {
        await using var agent = Agent(new ScriptedModel(), new RecordingTools());
        await agent.StartAsync("ollama", "/checkout", new Dictionary<string, string>(), CancellationToken.None);

        var initialized = await agent.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(initialized.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(initialized.GetProperty("authMethods").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public async Task Prompt_asksPermissionRunsThePinnedToolAndCompletes()
    {
        var model = new ScriptedModel(
            Reply(Call("browser_screenshot")),
            Reply(Call("complete_task")));
        var tools = new RecordingTools();
        await using var agent = Agent(model, tools);
        var permissions = new List<string>();
        agent.Request += (_, payload) =>
        {
            permissions.Add(payload.GetProperty("toolCall").GetProperty("kind").GetString()!);
            return Task.FromResult(JsonSerializer.SerializeToElement(new { outcome = new { outcome = "selected", optionId = "allow_once" } }));
        };
        var updates = new List<string>();
        agent.Notification += (_, payload) =>
        {
            updates.Add(payload.GetProperty("update").GetProperty("sessionUpdate").GetString()!);
            return Task.CompletedTask;
        };

        var result = await PromptAsync(agent, "review the dashboard");

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("completed").GetBoolean(), Is.True);
            Assert.That(tools.Calls, Is.EqualTo(new[] { "pm-1:browser_screenshot", "pm-1:complete_task" }));
            Assert.That(permissions, Is.EqualTo(new[] { "fetch" }));
            Assert.That(updates, Does.Contain("tool_call").And.Contain("tool_call_update"));
            Assert.That(model.Seen[1].Last().Images, Is.EqualTo(new[] { "c2NyZWVu" }));
        });
    }

    [Test]
    public async Task Prompt_refusedPermissionNeverReachesTheTool()
    {
        var tools = new RecordingTools();
        await using var agent = Agent(new ScriptedModel(Reply(Call("browser_click")), Reply(null)), tools);
        agent.Request += (_, _) => Task.FromResult(
            JsonSerializer.SerializeToElement(new { outcome = new { outcome = "selected", optionId = "reject_once" } }));

        var result = await PromptAsync(agent, "click save");

        Assert.Multiple(() =>
        {
            Assert.That(tools.Calls, Is.Empty);
            Assert.That(result.GetProperty("stopReason").GetString(), Is.EqualTo("end_turn"));
            Assert.That(result.GetProperty("completed").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task Prompt_aToolThatWasNotOfferedIsRefusedToTheModel()
    {
        var model = new ScriptedModel(Reply(Call("enqueue_commit")), Reply(null));
        var tools = new RecordingTools();
        await using var agent = Agent(model, tools);

        await PromptAsync(agent, "commit it");

        Assert.Multiple(() =>
        {
            Assert.That(tools.Calls, Is.Empty);
            Assert.That(model.Seen[1].Last().Content, Does.Contain("not available"));
        });
    }

    [Test]
    public async Task Prompt_stopsAtTheToolRoundBudget()
    {
        var model = new ScriptedModel(Enumerable.Range(0, 5).Select(_ => Reply(Call("complete_task_not"))).ToArray());
        await using var agent = Agent(model, new RecordingTools(), maxToolRounds: 2);

        var result = await PromptAsync(agent, "loop");

        Assert.That(result.GetProperty("stopReason").GetString(), Is.EqualTo("max_turn_requests"));
    }

    private static async Task<JsonElement> PromptAsync(InProcessAgentProcessProvider agent, string text)
    {
        await agent.StartAsync("ollama", "/checkout", new Dictionary<string, string>(), CancellationToken.None);
        var session = await agent.CallAsync("session/new", new { cwd = "/checkout" }, CancellationToken.None);
        return await agent.CallAsync("session/prompt", new
        {
            sessionId = session.GetProperty("sessionId").GetString(),
            prompt = new[] { new { type = "text", text } }
        }, CancellationToken.None);
    }

    private static InProcessAgentProcessProvider Agent(ScriptedModel model, RecordingTools tools, int maxToolRounds = 24) =>
        new("pm-1", model, tools, NullLogger<InProcessAgentProcessProvider>.Instance, maxToolRounds);

    private static InProcessChatMessage Reply(InProcessToolCall? call) =>
        new("assistant", call is null ? "Looks fine." : "", call is null ? null : [call]);

    private static InProcessToolCall Call(string name) => new(name, JsonSerializer.SerializeToElement(new { }));

    private sealed class ScriptedModel(params InProcessChatMessage[] replies) : IInProcessChatModel
    {
        private int _next;

        public List<IReadOnlyList<InProcessChatMessage>> Seen { get; } = [];

        public bool Configured => true;

        public Task<InProcessChatMessage> ChatAsync(
            IReadOnlyList<InProcessChatMessage> messages,
            IReadOnlyList<InProcessTool> tools,
            CancellationToken cancellationToken)
        {
            Seen.Add([.. messages]);
            return Task.FromResult(_next < replies.Length ? replies[_next++] : Reply(null));
        }
    }

    private sealed class RecordingTools : IInProcessToolHost
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<InProcessTool>> ListAsync(string workspaceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<InProcessTool>>([
                Tool("browser_screenshot"),
                Tool("browser_click"),
                Tool("complete_task"),
                Tool("complete_task_not")
            ]);

        public Task<InProcessToolResult> CallAsync(string workspaceId, string tool, JsonElement arguments, CancellationToken cancellationToken)
        {
            Calls.Add($"{workspaceId}:{tool}");
            return Task.FromResult(tool switch
            {
                "complete_task" => new InProcessToolResult("done", [], false, CompletesTask: true),
                "browser_screenshot" => new InProcessToolResult("captured", ["c2NyZWVu"], false),
                _ => new InProcessToolResult("ok", [], false)
            });
        }

        private static InProcessTool Tool(string name) =>
            new(name, name, JsonSerializer.SerializeToElement(new { type = "object" }));
    }
}
