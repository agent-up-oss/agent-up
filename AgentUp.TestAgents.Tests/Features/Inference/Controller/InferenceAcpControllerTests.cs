using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Controllers;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Features.Inference.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Inference.Controller;

[TestFixture]
public sealed class InferenceAcpControllerTests
{
    [Test]
    public async Task ServeAsync_advertisesNoAuthMethodsSoSessionNewIsImmediate()
    {
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Controller().ServeAsync(input, output, error, CancellationToken.None);
        var methods = JsonNode.Parse(output.ToString().Trim())!["result"]!["authMethods"]!.AsArray();

        Assert.That(methods, Is.Empty);
    }

    [Test]
    public async Task ServeAsync_completesATaskAfterThePolicyAllowsTheTool()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tagged = "<tool_call>{\"name\":\"complete_task\",\"arguments\":{\"summary\":\"done\"}}</tool_call>";
        var serving = Controller(tagged).ServeAsync(input, output, error, lifetime.Token);

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
    public async Task ServeAsync_endsTheTurnWhenPermissionIsRejected()
    {
        using var input = new LinePipe();
        using var output = new AgentOutput();
        using var error = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tagged = "<tool_call>{\"name\":\"complete_task\",\"arguments\":{\"summary\":\"done\"}}</tool_call>";
        var serving = Controller(tagged).ServeAsync(input, output, error, lifetime.Token);

        input.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""");
        await output.WaitForLineContainingAsync("protocolVersion", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"session/new","params":{"cwd":"."}}""");
        await output.WaitForLineContainingAsync("infer-session", lifetime.Token);
        input.WriteLine("""{"jsonrpc":"2.0","id":3,"method":"session/prompt","params":{"prompt":[{"type":"text","text":"finish"}]}}""");
        var permission = await output.WaitForLineContainingAsync("session/request_permission", lifetime.Token);
        var requestId = JsonNode.Parse(permission)!["id"]!.GetValue<int>();
        input.WriteLine(
            "{\"jsonrpc\":\"2.0\",\"id\":" + requestId +
            ",\"result\":{\"outcome\":{\"outcome\":\"selected\",\"optionId\":\"reject-once\"}}}");
        input.Complete();
        await serving;

        Assert.That(output.ToString(), Does.Contain("\"stopReason\":\"end_turn\""));
    }

    private static InferenceAcpController Controller(params string[] completions) => new(
        new LocalInferenceAcpService(
            new ScriptedTokenGenerator(completions),
            new ToolCallParser(),
            new WorktreeToolExecutor(),
            new FixedLoopInferenceSettings(42)));
}
