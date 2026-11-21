using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Inference.Unit;

[TestFixture]
public sealed class OpenAiCompatibleModelServiceTests
{
    private const string WriteTool =
        """{"type":"function","function":{"name":"write","description":"Writes a file.","parameters":{"type":"object","properties":{"filePath":{"type":"string"}}}}}""";

    [Test]
    public async Task A_plain_request_gets_the_tool_call_the_model_generated()
    {
        var generator = new ScriptedTokenGenerator("<tool_call>{\"name\": \"write\", \"arguments\": {\"filePath\": \"a.txt\"}}</tool_call>");

        var whole = await ModelServerComposition.Service(generator).CompleteAsync(
            $$"""{"model":"any","messages":[{"role":"user","content":"write a.txt"}],"tools":[{{WriteTool}}]}""",
            _ => Task.CompletedTask,
            CancellationToken.None);

        var call = JsonNode.Parse(whole!)!["choices"]![0]!["message"]!["tool_calls"]![0]!["function"]!;
        Assert.That(call["name"]!.GetValue<string>(), Is.EqualTo("write"));
        Assert.That(generator.LastPrompt, Does.Contain("write a.txt"));
    }

    [Test]
    public async Task A_streaming_request_is_answered_through_the_chunk_sink_only()
    {
        var chunks = new List<string>();

        var whole = await ModelServerComposition.Service(new ScriptedTokenGenerator("hello")).CompleteAsync(
            """{"model":"any","stream":true,"messages":[{"role":"user","content":"hi"}]}""",
            chunk =>
            {
                chunks.Add(chunk);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.That(whole, Is.Null);
        Assert.That(chunks[0], Does.Contain("\"content\":\"hello\""));
        Assert.That(chunks[^1], Does.Contain("\"finish_reason\":\"stop\""));
    }

    [Test]
    public void A_body_that_is_not_json_is_a_json_error()
    {
        Assert.ThrowsAsync(Is.InstanceOf<JsonException>(), () => ModelServerComposition.Service(new ScriptedTokenGenerator())
            .CompleteAsync("not json", _ => Task.CompletedTask, CancellationToken.None));
    }
}
