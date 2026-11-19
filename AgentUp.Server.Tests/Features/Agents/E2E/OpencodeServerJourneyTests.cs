using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// The whole OpenCode journey against a real, separately launched Agent-Up Server process, driven
/// only through the REST routes and the agent event stream Desktop and Mobile use. The Server
/// starts with nothing enabled; the test enables the built-in OpenCode capability, saves the
/// endpoint, token, and model a user would type into Settings, starts an OpenCode session, and
/// answers OpenCode's permission request the way the Allow once button does. The model backend is
/// <c>test-model-server</c>: the pinned CPU model behind an Ollama/OpenAI-compatible API that
/// refuses any request without the configured token. The model's skill is beside the point - what
/// is proven is that every hop from Settings to the model and back to the worktree is wired.
/// <c>.github/scripts/run-opencode-inference.sh</c> publishes the Server, installs OpenCode, and
/// starts the model server.
/// </summary>
[TestFixture]
[Category("OpencodeInference")]
[Timeout(1_200_000)]
[CancelAfter(1_200_000)]
public sealed class OpencodeServerJourneyTests
{
    private const string Content = "written through agent-up";

    private static readonly string[] Prompts =
    [
        $"Create a file named result.txt containing exactly: {Content}",
        $"Use the write tool now to create result.txt with the content: {Content}",
        $"Call the write tool with filePath result.txt and content {Content}. Do not use any other tool."
    ];
    private static readonly string[] McpPrompts =
    [
        "Call the list_workspaces tool to show the Agent-Up workspaces.",
        "Use the agent-up-orchestration_list_workspaces tool now.",
        "Call agent-up-orchestration_list_workspaces with no arguments. Do not use any other tool."
    ];

    private const string Steering = "Then call get_workspace_status for this workspace.";

    private static readonly TimeSpan TurnBudget = TimeSpan.FromMinutes(6);

    [Test]
    public async Task Settings_take_OpenCode_from_needs_setup_to_ready_without_echoing_the_token()
    {
        var inputs = OpencodeJourneyInputs.Read();
        await using var server = await AgentUpServerProcess.StartAsync(inputs);

        var before = await server.CapabilityAsync("opencode");
        using var enable = await server.Http.PostAsJsonAsync("api/capabilities/enable", new { id = "opencode" });
        var enabled = await server.CapabilityAsync("opencode");
        using var saved = await server.SaveSettingsAsync(inputs.Endpoint, inputs.ApiKey);
        var savedJson = await saved.Content.ReadAsStringAsync();
        var ready = await server.CapabilityAsync("opencode");

        Assert.That(before["enabled"]!.GetValue<bool>(), Is.False, "A fresh Server starts with OpenCode disabled.");
        Assert.That(enable.StatusCode, Is.EqualTo(HttpStatusCode.OK), await enable.Content.ReadAsStringAsync());
        Assert.That(enabled["state"]!.GetValue<string>(), Is.EqualTo("needsSetup"), "OpenCode cannot run before its endpoint is set.");
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK), savedJson);
        Assert.That(ready["state"]!.GetValue<string>(), Is.EqualTo("ready"));
        Assert.That(SettingNamed(ready, "token")["hasValue"]!.GetValue<bool>(), Is.True);
        Assert.That(savedJson + ready.ToJsonString(), Does.Not.Contain(inputs.ApiKey), "The token must never come back to a client.");
    }

    [Test]
    public async Task OpenCode_writes_the_file_after_the_user_allows_it_once()
    {
        var inputs = OpencodeJourneyInputs.Read();
        await using var server = await AgentUpServerProcess.StartAsync(inputs);
        using var worktree = new JourneyWorktree();
        await server.EnableOpencodeAsync(inputs.Endpoint, inputs.ApiKey);
        var workspaceId = await server.RegisterAsync(worktree.Path);

        using var schedule = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent", new { agent = "opencode" });
        Assert.That(schedule.StatusCode, Is.EqualTo(HttpStatusCode.OK), await schedule.Content.ReadAsStringAsync());
        // Subscribed after scheduling, as the clients do: the stream replays from the first event.
        await using var events = await AgentEventStream.OpenAsync(server.Http, workspaceId);
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "ready", TurnBudget, server);
        // The pinned CPU model is weak: it sometimes answers with a malformed or unrelated tool call
        // instead of writing. A person would ask again, so the journey does too, a bounded number of
        // times; every attempt still has to end visibly, and what is asserted below is the wiring.
        JsonNode? request = null;
        var prompts = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < Prompts.Length && request is null; attempt++)
        {
            using var prompt = await server.Http.PostAsJsonAsync(
                $"api/workspaces/{workspaceId}/agent/messages",
                new { message = Prompts[attempt] });
            prompts.Add(prompt.StatusCode);
            var next = await events.WaitForFirstAsync(["permission_request", "prompt_result"], TurnBudget, server);
            if (next.Type == "permission_request")
                request = next.Payload;
        }

        var asked = request ?? throw new AssertionException($"OpenCode never asked to write in {Prompts.Length} prompts. Seen: {string.Join(", ", events.Seen)}");
        var requestId = asked["requestId"]!.GetValue<string>();
        var options = asked["request"]!["options"]!.AsArray().Select(option => option!["optionId"]!.GetValue<string>()).ToList();
        using var decide = await server.Http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/agent/permissions",
            new { requestId, optionId = "once" });
        var decision = await events.WaitForAsync("permission_decision", payload => payload["requestId"]?.GetValue<string>() == requestId, TurnBudget, server);
        var result = await events.WaitForAsync("prompt_result", _ => true, TurnBudget, server);
        TestContext.Out.WriteLine($"opencode-journey stop={result.ToJsonString()} options={string.Join(",", options)}");

        Assert.That(prompts, Has.All.EqualTo(HttpStatusCode.Accepted));
        Assert.That(options, Does.Contain("once").And.Contain("reject"), "OpenCode offers its own permission options.");
        Assert.That(decide.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(decision["optionId"]!.GetValue<string>(), Is.EqualTo("once"), "Every client sees the request close with the chosen option.");
        Assert.That(worktree.Read("result.txt"), Is.EqualTo(Content), "OpenCode did not write result.txt into the workspace.");
        Assert.That(events.ToolUpdates.Any(WritesTheContent), Is.True, "The transcript draws a write as a diff from the content the tool reported; OpenCode must report it.");
    }

    /// <summary>
    /// The same request from a dev reviewer, approved the same way, writes nothing: the Server
    /// starts the reviewer's OpenCode with its clone mounted read-only, so the kernel refuses the
    /// write whatever the agent or the person answering the prompt decides.
    /// </summary>
    [Test]
    public async Task A_dev_reviewers_OpenCode_cannot_write_its_clone_even_when_allowed()
    {
        var inputs = OpencodeJourneyInputs.Read();
        await using var server = await AgentUpServerProcess.StartAsync(inputs);
        using var worktree = new JourneyWorktree();
        await server.EnableOpencodeAsync(inputs.Endpoint, inputs.ApiKey);
        var workspaceId = await server.RegisterAsync(worktree.Path);
        using var member = await server.Http.PutAsJsonAsync($"api/workspaces/{workspaceId}/agent/member", new { kind = "DevReviewer" });

        using var schedule = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent", new { agent = "opencode" });
        Assert.That(schedule.StatusCode, Is.EqualTo(HttpStatusCode.OK), await schedule.Content.ReadAsStringAsync());
        await using var events = await AgentEventStream.OpenAsync(server.Http, workspaceId);
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "ready", TurnBudget, server);
        var allowed = 0;
        for (var attempt = 0; attempt < Prompts.Length && allowed == 0; attempt++)
        {
            using var prompt = await server.Http.PostAsJsonAsync(
                $"api/workspaces/{workspaceId}/agent/messages",
                new { message = Prompts[attempt] });
            var next = await events.WaitForFirstAsync(["permission_request", "prompt_result"], TurnBudget, server);
            if (next.Type != "permission_request")
                continue;
            using var decide = await server.Http.PostAsJsonAsync(
                $"api/workspaces/{workspaceId}/agent/permissions",
                new { requestId = next.Payload["requestId"]!.GetValue<string>(), optionId = "once" });
            allowed++;
            await events.WaitForAsync("prompt_result", _ => true, TurnBudget, server);
        }

        Assert.That(member.StatusCode, Is.EqualTo(HttpStatusCode.OK), await member.Content.ReadAsStringAsync());
        Assert.That(allowed, Is.EqualTo(1), $"OpenCode never asked to write in {Prompts.Length} prompts. Seen: {string.Join(", ", events.Seen)}");
        Assert.That(File.Exists(System.IO.Path.Join(worktree.Path, "result.txt")), Is.False, "A dev reviewer's clone is read-only.");
        Assert.That(events.ToolUpdates.Any(update => update["status"]?.GetValue<string>() == "failed"), Is.True,
            "OpenCode reports the refused write as a failed tool call.");
    }

    /// <summary>
    /// What Desktop and Chat draw an edit's diff from: the written content in the input, ACP's diff
    /// content item, or the unified diff in the tool's metadata.
    /// </summary>
    private static bool WritesTheContent(JsonNode update) =>
        update["rawInput"]?["content"]?.GetValue<string>()?.Contains(Content, StringComparison.Ordinal) == true
        || update["content"]?.AsArray().Any(item => item?["type"]?.GetValue<string>() == "diff" && item["newText"]?.GetValue<string>()?.Contains(Content, StringComparison.Ordinal) == true) == true
        || update["rawOutput"]?["metadata"]?["diff"]?.GetValue<string>()?.Contains(Content, StringComparison.Ordinal) == true;

    [Test]
    public async Task OpenCode_calls_Agent_Up_mcp_tools_and_takes_steering_mid_turn()
    {
        var inputs = OpencodeJourneyInputs.Read();
        await using var server = await AgentUpServerProcess.StartAsync(inputs);
        using var worktree = new JourneyWorktree();
        await server.EnableOpencodeAsync(inputs.Endpoint, inputs.ApiKey);
        var workspaceId = await server.RegisterAsync(worktree.Path);
        using var schedule = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent", new { agent = "opencode" });
        await using var events = await AgentEventStream.OpenAsync(server.Http, workspaceId);
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "ready", TurnBudget, server);

        // Ask for an Agent-Up tool, and steer while that turn is still running, as a person
        // typing a follow-up into the chat would.
        using var prompt = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent/messages", new { message = McpPrompts[0] });
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "running", TurnBudget, server);
        using var steer = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent/messages", new { message = Steering });

        // Two turns follow: the asked one and the steered one. The weak model may still miss the
        // tool, so it is asked again a bounded number of times; only the tool call is retried.
        var turns = 0;
        var asked = 1;
        var steeringSeen = false;
        var mcpCalls = new List<string>();
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        while (turns < 2 || (mcpCalls.Count == 0 && asked < McpPrompts.Length))
        {
            if (turns >= 2)
            {
                using var again = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent/messages", new { message = McpPrompts[asked++] });
                turns--;
            }

            var next = await events.WaitForFirstAsync(["user_message", "session_update", "prompt_result"], TurnBudget, server);
            steeringSeen |= next.Type == "user_message" && next.Payload["steering"]?.GetValue<bool>() == true;
            if (next.Type == "prompt_result")
                turns++;
            // OpenCode names the tool on the first tool_call event and reports completion by id.
            if (next.Payload["update"] is { } update && update["toolCallId"]?.GetValue<string>() is { } callId)
            {
                if (update["title"]?.GetValue<string>() is { } title)
                    titles[callId] = title;
                if (update["status"]?.GetValue<string>() == "completed"
                    && titles.TryGetValue(callId, out var called)
                    && called.StartsWith("agent-up-", StringComparison.Ordinal))
                    mcpCalls.Add(called);
            }
        }

        TestContext.Out.WriteLine($"opencode-mcp calls={string.Join(",", mcpCalls)} asked={asked}");
        Assert.That(prompt.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        Assert.That(steer.StatusCode, Is.EqualTo(HttpStatusCode.Accepted), "A message sent mid-turn is steering, not a refusal.");
        Assert.That(steeringSeen, Is.True, "Every client is told the message steers the next turn.");
        Assert.That(turns, Is.GreaterThanOrEqualTo(2), "The steering ran as its own turn after the first one ended.");
        Assert.That(mcpCalls, Is.Not.Empty, $"OpenCode never completed an Agent-Up MCP tool call. Seen: {string.Join(", ", events.Seen)}");
    }

    [Test]
    public async Task A_wrong_token_reaches_the_model_backend_and_is_refused()
    {
        var inputs = OpencodeJourneyInputs.Read();
        await using var server = await AgentUpServerProcess.StartAsync(inputs);
        using var worktree = new JourneyWorktree();
        await server.EnableOpencodeAsync(inputs.Endpoint, "not-the-token");
        var workspaceId = await server.RegisterAsync(worktree.Path);

        using var schedule = await server.Http.PostAsJsonAsync($"api/workspaces/{workspaceId}/agent", new { agent = "opencode" });
        await using var events = await AgentEventStream.OpenAsync(server.Http, workspaceId);
        await events.WaitForAsync("state", payload => payload["state"]?.GetValue<string>() == "ready", TurnBudget, server);
        using var prompt = await server.Http.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/agent/messages",
            new { message = "Create a file named result.txt containing exactly: refused" });
        // The model server answers a wrong token 401; OpenCode reports it and the turn ends with
        // that error on the session rather than a silent hang.
        var ended = await events.WaitForAsync("state", payload => payload["error"]?.GetValue<string>() is { Length: > 0 }, TurnBudget, server);
        var transcript = events.Seen;
        TestContext.Out.WriteLine($"opencode-wrong-token error={ended["error"]}");

        Assert.That(schedule.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(prompt.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        Assert.That(ended["error"]!.GetValue<string>(), Does.Contain("Invalid API key"), "The model backend's own refusal has to reach the client.");
        Assert.That(worktree.Exists("result.txt"), Is.False, "Nothing may be written when the model refused the token.");
        Assert.That(transcript, Has.None.EqualTo("permission_request"), "A refused model cannot have asked for a tool.");
    }

    private static JsonNode SettingNamed(JsonNode capability, string key) =>
        capability["settings"]!.AsArray().Single(setting => setting!["key"]!.GetValue<string>() == key)!;
}

/// <summary>What the inference script hands the journey: OpenCode, the model endpoint and its token, and the published Server.</summary>
internal sealed record OpencodeJourneyInputs(string Opencode, string Endpoint, string ApiKey, string ServerAssembly)
{
    public static OpencodeJourneyInputs Read() => new(
        Required("AGENTUP_OPENCODE_INFERENCE_BINARY"),
        Required("AGENTUP_OPENCODE_INFERENCE_ENDPOINT"),
        Required("AGENTUP_OPENCODE_INFERENCE_API_KEY"),
        Required("AGENTUP_OPENCODE_INFERENCE_SERVER"));

    private static string Required(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set; run .github/scripts/run-opencode-inference.sh.");
}

/// <summary>A real Agent-Up Server process with its own empty data directory and authentication off.</summary>
internal sealed class AgentUpServerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _dataDirectory;
    private readonly StringBuilder _output = new();
    private readonly HttpClient _http;

    private AgentUpServerProcess(Process process, string dataDirectory, int port)
    {
        _process = process;
        _dataDirectory = dataDirectory;
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromMinutes(2) };
    }

    public HttpClient Http => _http;

    public string Output
    {
        get
        {
            lock (_output)
                return _output.ToString();
        }
    }

    public static async Task<AgentUpServerProcess> StartAsync(OpencodeJourneyInputs inputs)
    {
        var dataDirectory = Directory.CreateTempSubdirectory("agent-up-opencode-journey-data").FullName;
        var port = FreePort();
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { inputs.ServerAssembly },
            WorkingDirectory = Path.GetDirectoryName(inputs.ServerAssembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Storage__DataDirectory"] = dataDirectory;
        start.Environment["AGENTUP_AUTH_DISABLED"] = "true";
        start.Environment["AGENTUP_CAPABILITY_ENABLED_PATH"] = Path.Join(dataDirectory, "seed-enabled.json");
        // The installed OpenCode, not a Nix shell: the capability still supplies `acp` and the
        // generated provider configuration, which is the part under test.
        start.Environment["Agents__opencode__Command"] = inputs.Opencode;
        var process = Process.Start(start) ?? throw new InvalidOperationException("The Server process did not start.");
        var server = new AgentUpServerProcess(process, dataDirectory, port);
        process.OutputDataReceived += (_, line) => server.Append(line.Data);
        process.ErrorDataReceived += (_, line) => server.Append(line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await server.WaitUntilAnsweringAsync();
        return server;
    }

    public async Task<JsonNode> CapabilityAsync(string id)
    {
        var all = await Http.GetFromJsonAsync<JsonArray>("api/capabilities") ?? [];
        return all.Single(module => module!["id"]!.GetValue<string>() == id)!;
    }

    public Task<HttpResponseMessage> SaveSettingsAsync(string endpoint, string token) =>
        Http.PutAsJsonAsync("api/capabilities/opencode/settings", new
        {
            values = new Dictionary<string, string?>
            {
                ["endpoint"] = endpoint,
                ["token"] = token,
                ["model"] = "qwen2.5-1.5b-instruct"
            }
        });

    public async Task EnableOpencodeAsync(string endpoint, string token)
    {
        using var enable = await Http.PostAsJsonAsync("api/capabilities/enable", new { id = "opencode" });
        if (enable.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Enabling OpenCode failed: {await enable.Content.ReadAsStringAsync()}");
        using var saved = await SaveSettingsAsync(endpoint, token);
        if (saved.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Saving OpenCode settings failed: {await saved.Content.ReadAsStringAsync()}");
    }

    public async Task<string> RegisterAsync(string worktree)
    {
        using var response = await Http.PostAsJsonAsync("api/workspaces", new
        {
            displayName = "OpenCode journey",
            repositoryPath = worktree,
            worktreePath = worktree,
            branch = "main",
            commit = "0000000"
        });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Registering the workspace failed ({(int)response.StatusCode}): {body}");
        return JsonNode.Parse(body)!["id"]!.GetValue<string>();
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
        TestContext.Out.WriteLine($"--- Agent-Up Server output ---{Environment.NewLine}{Tail(Output)}");
        try { Directory.Delete(_dataDirectory, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.Out.WriteLine($"Could not remove {_dataDirectory}: {exception.Message}");
        }
    }

    private async Task WaitUntilAnsweringAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        var lastError = "no attempt yet";
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"The Server exited with {_process.ExitCode} before answering:{Environment.NewLine}{Output}");
            try
            {
                using var status = await Http.GetAsync("api/auth/status");
                if (status.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException exception)
            {
                // Not listening yet; the deadline bounds this and the timeout reports the last error.
                lastError = exception.Message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"The Server did not answer within two minutes ({lastError}):{Environment.NewLine}{Output}");
    }

    private void Append(string? line)
    {
        if (line is null)
            return;
        lock (_output)
            _output.AppendLine(line);
    }

    private static string Tail(string text) => text.Length <= 6000 ? text : text[^6000..];

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

/// <summary>The agent event stream a client subscribes to, read frame by frame.</summary>
internal sealed class AgentEventStream : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly StreamReader _reader;
    private readonly List<string> _seen = [];
    private readonly List<(string Type, JsonNode Payload)> _frames = [];

    private AgentEventStream(HttpResponseMessage response, StreamReader reader)
    {
        _response = response;
        _reader = reader;
    }

    public IReadOnlyList<string> Seen => _seen;

    /// <summary>Every frame read so far, including the ones a wait passed over.</summary>
    public IReadOnlyList<(string Type, JsonNode Payload)> Frames => _frames;

    /// <summary>The ACP tool updates read so far, unwrapped from their session envelope.</summary>
    public IEnumerable<JsonNode> ToolUpdates => _frames
        .Where(frame => frame.Type == "session_update")
        .Select(frame => frame.Payload["update"] ?? frame.Payload)
        .Where(update => update["sessionUpdate"]?.GetValue<string>() is "tool_call" or "tool_call_update");

    public static async Task<AgentEventStream> OpenAsync(HttpClient http, string workspaceId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/workspaces/{workspaceId}/agent/events");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return new AgentEventStream(response, new StreamReader(await response.Content.ReadAsStreamAsync()));
    }

    public async Task<JsonNode> WaitForAsync(string type, Func<JsonNode, bool> matches, TimeSpan budget, AgentUpServerProcess server) =>
        (await NextAsync(candidate => candidate.Type == type && matches(candidate.Payload), $"a '{type}' event", budget, server)).Payload;

    /// <summary>The next event of any of <paramref name="types"/>, with which one it was.</summary>
    public Task<(string Type, JsonNode Payload)> WaitForFirstAsync(IReadOnlyList<string> types, TimeSpan budget, AgentUpServerProcess server) =>
        NextAsync(candidate => types.Contains(candidate.Type), $"any of {string.Join(", ", types)}", budget, server);

    public async ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _response.Dispose();
        await Task.CompletedTask;
    }

    private async Task<(string Type, JsonNode Payload)> NextAsync(Func<(string Type, JsonNode Payload), bool> matches, string what, TimeSpan budget, AgentUpServerProcess server)
    {
        using var deadline = new CancellationTokenSource(budget);
        try
        {
            while (true)
            {
                var frame = await ReadFrameAsync(deadline.Token);
                if (frame is null)
                    throw new InvalidOperationException($"The event stream ended while waiting for {what}. Seen: {string.Join(", ", _seen)}");
                if (matches(frame.Value))
                    return frame.Value;
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No {what} within {budget}. Seen: {string.Join(", ", _seen)}{Environment.NewLine}{server.Output}");
        }
    }

    private async Task<(string Type, JsonNode Payload)?> ReadFrameAsync(CancellationToken cancellationToken)
    {
        string? type = null;
        string? data = null;
        while (await _reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
                type = line["event: ".Length..];
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
                data = line["data: ".Length..];
            else if (line.Length == 0 && type is not null && data is not null)
            {
                _seen.Add(type);
                TestContext.Out.WriteLine($"event {type}: {(data.Length > 1500 ? data[..1500] + "..." : data)}");
                var payload = JsonNode.Parse(data)!["payload"]!;
                _frames.Add((type, payload));
                return (type, payload);
            }
        }

        return null;
    }
}

/// <summary>A throwaway Git worktree the journey registers as its workspace.</summary>
internal sealed class JourneyWorktree : IDisposable
{
    public JourneyWorktree()
    {
        Path = Directory.CreateTempSubdirectory("agent-up-opencode-journey").FullName;
        File.WriteAllText(System.IO.Path.Join(Path, "README.md"), "opencode journey worktree\n");
    }

    public string Path { get; }

    public bool Exists(string name) => File.Exists(System.IO.Path.Join(Path, name));

    public string? Read(string name) => Exists(name) ? File.ReadAllText(System.IO.Path.Join(Path, name)).Trim() : null;

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.Out.WriteLine($"Could not remove {Path}: {exception.Message}");
        }
    }
}
