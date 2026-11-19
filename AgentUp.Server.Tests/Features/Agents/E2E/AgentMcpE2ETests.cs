using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// An ACP agent driving Agent-Up through the MCP servers the Server offers it, with nothing
/// faked: a real Server process, the sandboxed <c>test-agent-scripted</c> making real Streamable
/// HTTP MCP calls, a real application the agent starts, and the real headless browser it then
/// navigates, inspects and clicks. Each prompt is a directive naming the tool calls to make, so
/// the run is deterministic. The agent never names its workspace: the session credential the
/// Server hands it pins every call to its own workspace and to that workspace's team member
/// grant, which is how a dev reviewer is refused orchestration, the browser and the commit queue.
/// <c>.github/scripts/run-agent-e2e.sh</c> publishes the test agents and supplies bubblewrap.
/// </summary>
[TestFixture]
[Category("AgentMcpE2E")]
[NonParallelizable]
[Timeout(600_000)]
[CancelAfter(600_000)]
public sealed class AgentMcpE2ETests
{
    private const string Agent = "test-agent-scripted";
    private static readonly TimeSpan TurnBudget = TimeSpan.FromMinutes(3);

    [Test]
    public async Task An_author_runs_its_app_and_drives_it_in_the_browser_through_mcp()
    {
        await using var server = await StartServerAsync();
        using var app = new McpJourneyApp();
        var workspaceId = await server.RegisterAsync(app.Path, "MCP journey");
        await using var events = await StartAgentAsync(server, workspaceId);

        var started = await TurnAsync(server, workspaceId, events, Call("agent-up-orchestration", "start_workspace"));
        var port = await RunningPortAsync(server, workspaceId);
        var browsed = await TurnAsync(server, workspaceId, events,
            Call("agent-up-browser", "browser_navigate", new JsonObject { ["url"] = $"http://127.0.0.1:{port}/" }),
            Call("agent-up-browser", "browser_inspect"),
            Call("agent-up-browser", "browser_click", new JsonObject { ["selector"] = "#go" }),
            Call("agent-up-browser", "browser_wait_for_text", new JsonObject { ["text"] = "Clicked" }),
            Call("agent-up-audit", "audit_query", new JsonObject { ["limit"] = 5 }),
            Call("agent-up-training", "train_decision_categories"));

        Assert.That(started.Single().Status, Is.EqualTo("completed"), started.Single().Output);
        Assert.That(browsed.Select(call => $"{call.Title}={call.Status}"), Has.All.EndsWith("=completed"),
            string.Join(Environment.NewLine, browsed.Select(call => $"{call.Title}: {call.Output}")));
        Assert.That(browsed[1].Output, Does.Contain("Harbor").And.Contain("Go"), "browser_inspect reads the page the app serves.");
        Assert.That(browsed[3].Output, Does.Contain("Clicked"), "The click ran the page's script.");
    }

    [Test]
    public async Task An_agents_mcp_session_is_pinned_to_its_own_workspace()
    {
        await using var server = await StartServerAsync();
        using var app = new McpJourneyApp();
        using var other = new McpJourneyApp();
        var workspaceId = await server.RegisterAsync(app.Path, "MCP journey");
        var otherId = await server.RegisterAsync(other.Path, "Another member");
        await using var events = await StartAgentAsync(server, workspaceId);

        var calls = await TurnAsync(server, workspaceId, events,
            Call("agent-up-orchestration", "list_workspaces"),
            Call("agent-up-orchestration", "get_workspace_status", new JsonObject { ["id"] = otherId }));

        Assert.That(calls[0].Status, Is.EqualTo("completed"), calls[0].Output);
        Assert.That(calls[0].Output, Does.Contain(workspaceId).And.Not.Contain(otherId), "A pinned session lists only its own workspace.");
        Assert.That(calls[1].Status, Is.EqualTo("failed"), "Naming another workspace is refused.");
    }

    [Test]
    public async Task A_dev_reviewer_reads_but_cannot_run_the_app_drive_the_browser_or_queue_commits()
    {
        await using var server = await StartServerAsync();
        using var app = new McpJourneyApp();
        var workspaceId = await server.RegisterAsync(app.Path, "MCP journey");
        using var member = await server.Http.PutAsJsonAsync($"api/workspaces/{workspaceId}/agent/member", new { kind = "DevReviewer" });
        await using var events = await StartAgentAsync(server, workspaceId);

        var calls = await TurnAsync(server, workspaceId, events,
            Call("agent-up-orchestration", "get_workspace_status"),
            Call("agent-up-orchestration", "start_workspace"),
            Call("agent-up-browser", "browser_navigate", new JsonObject { ["url"] = "http://127.0.0.1:9/" }),
            Call("agent-up-commits", "enqueue_commit", new JsonObject
            {
                ["slice"] = "Docs",
                ["message"] = "docs(Docs): reviewer change",
                ["files"] = new JsonArray { "README.md" }
            }));

        Assert.That(member.StatusCode, Is.EqualTo(HttpStatusCode.OK), await member.Content.ReadAsStringAsync());
        Assert.That(calls[0].Status, Is.EqualTo("completed"), calls[0].Output);
        Assert.That(calls.Skip(1).Select(call => $"{call.Title}={call.Status}"), Has.All.EndsWith("=failed"),
            string.Join(Environment.NewLine, calls.Select(call => $"{call.Title}: {call.Output}")));
        Assert.That(await WorkspaceStateAsync(server, workspaceId), Is.Not.EqualTo("Running"), "The reviewer could not start the app.");
    }

    private static Task<AgentUpServerProcess> StartServerAsync()
    {
        var environment = new Dictionary<string, string>
        {
            [$"Agents__{Agent}__Command"] = Required("AGENTUP_AGENT_E2E_TEST_AGENTS"),
            [$"Agents__{Agent}__Arguments__0"] = "--agent",
            [$"Agents__{Agent}__Arguments__1"] = Agent,
            [$"Agents__{Agent}__Arguments__2"] = "acp"
        };
        if (Environment.GetEnvironmentVariable("AGENTUP_AGENT_E2E_CHROMIUM") is { Length: > 0 } chromium)
            environment["Browser__ExecutablePath"] = chromium;
        var serverAssembly = Path.Join(AppContext.BaseDirectory, "AgentUp.Server.dll");
        return AgentUpServerProcess.StartAsync(serverAssembly, environment, "Development");
    }

    private static async Task<AgentEventStream> StartAgentAsync(AgentUpServerProcess server, string workspaceId)
    {
        using var schedule = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent", new { agent = Agent });
        var body = await schedule.Content.ReadAsStringAsync();
        if (schedule.StatusCode != HttpStatusCode.OK)
            throw new AssertionException($"Starting {Agent} failed ({(int)schedule.StatusCode}): {body}{Environment.NewLine}{server.Output}");
        var events = await AgentEventStream.OpenAsync(server.Http, workspaceId);
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "ready", TurnBudget, server);
        return events;
    }

    /// <summary>Sends one directive and returns each call's final status and output, in order.</summary>
    private static async Task<IReadOnlyList<McpCallOutcome>> TurnAsync(
        AgentUpServerProcess server,
        string workspaceId,
        AgentEventStream events,
        params JsonObject[] calls)
    {
        var before = events.Frames.Count;
        var directive = new JsonObject { ["mcp"] = new JsonArray(calls.Select(call => (JsonNode)call).ToArray()) };
        using var prompt = await server.Http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/agent/messages",
            new { message = directive.ToJsonString() });
        if (prompt.StatusCode != HttpStatusCode.Accepted)
            throw new AssertionException($"The prompt was refused ({(int)prompt.StatusCode}): {await prompt.Content.ReadAsStringAsync()}");
        await events.WaitForAsync("prompt_result", _ => true, TurnBudget, server);
        return events.Frames
            .Skip(before)
            .Where(frame => frame.Type == "session_update")
            .Select(frame => frame.Payload["update"] ?? frame.Payload)
            .Where(update => update["sessionUpdate"]?.GetValue<string>() == "tool_call_update")
            .Select(update => new McpCallOutcome(
                update["title"]?.GetValue<string>() ?? string.Empty,
                update["status"]?.GetValue<string>() ?? string.Empty,
                update["rawOutput"]?["output"]?.GetValue<string>() ?? string.Empty))
            .ToArray();
    }

    /// <summary>The allocated HTTP port once the workspace reports its application running.</summary>
    private static async Task<int> RunningPortAsync(AgentUpServerProcess server, string workspaceId)
    {
        var deadline = DateTime.UtcNow + TurnBudget;
        JsonNode? workspace = null;
        while (DateTime.UtcNow < deadline)
        {
            workspace = await server.Http.GetFromJsonAsync<JsonNode>($"api/workspaces/{workspaceId}");
            var port = workspace?["applications"]?.AsArray()
                .SelectMany(application => application?["allocatedPorts"]?.AsArray() ?? [])
                .Select(entry => entry?["allocatedPort"]?.GetValue<int>())
                .FirstOrDefault(value => value is > 0);
            if (workspace?["state"]?.GetValue<string>() == "Running" && port is { } allocated)
                return allocated;
            if (workspace?["state"]?.GetValue<string>() == "Failed")
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
        throw new AssertionException($"The app never ran: {workspace?.ToJsonString()}{Environment.NewLine}{server.Output}");
    }

    private static async Task<string?> WorkspaceStateAsync(AgentUpServerProcess server, string workspaceId) =>
        (await server.Http.GetFromJsonAsync<JsonNode>($"api/workspaces/{workspaceId}"))?["state"]?.GetValue<string>();

    private static JsonObject Call(string server, string tool, JsonObject? arguments = null) => new()
    {
        ["server"] = server,
        ["tool"] = tool,
        ["arguments"] = arguments ?? new JsonObject()
    };

    private static string Required(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set; run .github/scripts/run-agent-e2e.sh.");

    private sealed record McpCallOutcome(string Title, string Status, string Output);
}
