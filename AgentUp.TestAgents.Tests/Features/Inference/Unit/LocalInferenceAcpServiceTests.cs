using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Features.Inference.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Inference.Unit;

[TestFixture]
public sealed class LocalInferenceAcpServiceTests
{
    [Test]
    public async Task RunAsync_printsTheSeedAndOpensASessionWithoutSigningIn()
    {
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n" +
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Agent().RunAsync(input, output, error, CancellationToken.None);
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Multiple(() =>
        {
            Assert.That(error.ToString(), Does.Contain("loop-inference seed=7"));
            Assert.That(JsonNode.Parse(lines[0])!["result"]!["authMethods"]!.AsArray(), Is.Empty);
            Assert.That(JsonNode.Parse(lines[1])!["result"]!["sessionId"]!.GetValue<string>(), Does.StartWith("infer-session-"));
        });
    }

    [Test]
    public async Task RunAsync_returnsEndTurnWhenTheToolIsUnknown()
    {
        using var input = new StringReader(PromptFrames("unused"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var tagged = "<tool_call>{\"name\":\"explode\",\"arguments\":{}}</tool_call>";

        await Agent(tagged).RunAsync(input, output, error, CancellationToken.None);

        Assert.That(LastResult(output)!["stopReason"]!.GetValue<string>(), Is.EqualTo("end_turn"));
    }

    [Test]
    public async Task RunAsync_returnsEndTurnWhenTheModelEmitsNoToolCall()
    {
        using var input = new StringReader(PromptFrames("just thinking out loud"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Agent("just thinking out loud").RunAsync(input, output, error, CancellationToken.None);
        var result = LastResult(output);

        Assert.That(result!["stopReason"]!.GetValue<string>(), Is.EqualTo("end_turn"));
    }

    [Test]
    public async Task RunAsync_skipsBrokenAndEmptyFrames()
    {
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n" +
            "\n" +
            "not-json\n" +
            "null\n" +
            """{"jsonrpc":"2.0"}""" + "\n" +
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Agent().RunAsync(input, output, error, CancellationToken.None);
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(JsonNode.Parse(lines[1])!["result"]!["sessionId"], Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_returnsEndTurnWhenThePromptHasNoTextItems()
    {
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n" +
            """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{}}""" + "\n" +
            """{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Agent("just thinking out loud").RunAsync(input, output, error, CancellationToken.None);

        Assert.That(LastResult(output)!["stopReason"]!.GetValue<string>(), Is.EqualTo("end_turn"));
    }

    [Test]
    public async Task RunAsync_returnsCancelledWhenSessionCancelStopsGeneration()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serving = Agent(TimeSpan.FromSeconds(5), "unused")
            .RunAsync(input, output, error, lifetime.Token);

        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        await output.WaitForLineContainingAsync("protocolVersion", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        await output.WaitForLineContainingAsync("infer-session", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"do it"}]}}""");
        input.WriteLine("""{"jsonrpc":"2.0","method":"session/cancel"}""");
        input.Complete();
        await serving;

        Assert.That(output.ToString(), Does.Contain("\"stopReason\":\"cancelled\""));
    }

    [Test]
    public async Task RunAsync_endsTheTurnWhenPermissionWaitIsCancelled()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tagged = "<tool_call>{\"name\":\"complete_task\",\"arguments\":{\"summary\":\"done\"}}</tool_call>";
        var serving = Agent(tagged).RunAsync(input, output, error, lifetime.Token);

        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        await output.WaitForLineContainingAsync("protocolVersion", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        await output.WaitForLineContainingAsync("infer-session", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"finish"}]}}""");
        await output.WaitForLineContainingAsync("session/request_permission", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","method":"session/cancel"}""");
        input.Complete();
        await serving;

        Assert.That(output.ToString(), Does.Contain("\"stopReason\":\"end_turn\""));
        Assert.That(output.ToString(), Does.Not.Contain("\"completed\":true"));
    }

    [Test]
    public async Task RunAsync_completesTheTaskWhenPermissionAllowsCompleteTask()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tagged = "<tool_call>{\"name\":\"complete_task\",\"arguments\":{\"summary\":\"done\"}}</tool_call>";
        var serving = Agent(tagged).RunAsync(input, output, error, lifetime.Token);

        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        await output.WaitForLineContainingAsync("protocolVersion", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        await output.WaitForLineContainingAsync("infer-session", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"finish"}]}}""");
        var permission = await output.WaitForLineContainingAsync("session/request_permission", lifetime.Token);
        var requestId = JsonNode.Parse(permission)!["id"]!.GetValue<int>();
        input.WriteLine(
            "{\"jsonrpc\":\"2.0\",\"id\":" + requestId +
            ",\"result\":{\"outcome\":{\"outcome\":\"selected\",\"optionId\":\"allow-once\"}}}");
        input.Complete();
        await serving;

        Assert.That(output.ToString(), Does.Contain("\"completed\":true"));
    }

    [Test]
    public async Task RunAsync_returnsMaxTurnRequestsAfterTheToolStepCap()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tagged = "<tool_call>{\"name\":\"list_files\",\"arguments\":{\"path\":\".\"}}</tool_call>";
        var completions = Enumerable.Repeat(tagged, LocalInferenceAcpService.MaxToolSteps).ToArray();
        var serving = Agent(completions).RunAsync(input, output, error, lifetime.Token);

        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        await output.WaitForLineContainingAsync("protocolVersion", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        await output.WaitForLineContainingAsync("infer-session", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"list"}]}}""");
        for (var step = 1; step <= LocalInferenceAcpService.MaxToolSteps; step++)
        {
            var requestId = await WaitForPermissionIdAsync(output, step, lifetime.Token);
            input.WriteLine(
                "{\"jsonrpc\":\"2.0\",\"id\":" + requestId +
                ",\"result\":{\"outcome\":{\"outcome\":\"selected\",\"optionId\":\"allow-once\"}}}");
        }

        input.Complete();
        await serving;

        Assert.That(output.ToString(), Does.Contain("\"stopReason\":\"max_turn_requests\""));
    }

    [Test]
    public async Task RunAsync_boundsSessionHistoryAcrossManyTurns()
    {
        var frames = new System.Text.StringBuilder();
        frames.AppendLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        frames.AppendLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        for (var prompt = 3; prompt <= 16; prompt++)
            frames.AppendLine(
                "{\"jsonrpc\":\"2.0\",\"id\":" + prompt +
                ",\"method\":\"session/prompt\",\"params\":{\"prompt\":[{\"type\":\"text\",\"text\":\"do it\"}]}}");
        using var input = new StringReader(frames.ToString());
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Agent("just thinking out loud").RunAsync(input, output, error, CancellationToken.None);

        Assert.That(LastResult(output)!["stopReason"]!.GetValue<string>(), Is.EqualTo("end_turn"));
    }

    private static LocalInferenceAcpService Agent(params string[] completions) =>
        Agent(TimeSpan.Zero, completions);

    private static LocalInferenceAcpService Agent(TimeSpan delay, params string[] completions) => new(
        new ScriptedTokenGenerator(completions) { Delay = delay },
        new ToolCallParser(),
        new WorktreeToolExecutor(),
        new FixedLoopInferenceSettings(7));

    private static string PromptFrames(string unused) =>
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n" +
        """{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""" + "\n" +
        """{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"do it"}]}}""" + "\n";

    private static JsonNode? LastResult(StringWriter output)
    {
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return JsonNode.Parse(lines[^1])!["result"];
    }

    private static async Task<int> WaitForPermissionIdAsync(
        AgentOutput output, int count, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = output.ToString()
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Contains("session/request_permission", StringComparison.Ordinal))
                .Select(line => JsonNode.Parse(line)!["id"]!.GetValue<int>())
                .ToArray();
            if (ids.Length >= count)
                return ids[count - 1];
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException("The agent never asked for the expected permission.");
    }
}
