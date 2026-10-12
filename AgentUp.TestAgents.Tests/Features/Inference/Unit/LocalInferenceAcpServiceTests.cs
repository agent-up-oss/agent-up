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

    private static LocalInferenceAcpService Agent(params string[] completions) => new(
        new ScriptedTokenGenerator(completions),
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
}
