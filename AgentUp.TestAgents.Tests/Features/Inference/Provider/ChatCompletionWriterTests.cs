using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Models;
using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class ChatCompletionWriterTests
{
    private static readonly ChatCompletion WithCall = new("", [new ChatToolCall("call_1", "write", "{\"filePath\":\"a\"}")], 7, 3);

    [Test]
    public void Whole_is_an_openai_chat_completion_with_tool_calls_and_usage()
    {
        var json = JsonNode.Parse(new ChatCompletionWriter().Whole("chatcmpl-1", "m", WithCall))!;
        var choice = json["choices"]![0]!;

        Assert.That(json["object"]!.GetValue<string>(), Is.EqualTo("chat.completion"));
        Assert.That(choice["finish_reason"]!.GetValue<string>(), Is.EqualTo("tool_calls"));
        Assert.That(choice["message"]!["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>(), Is.EqualTo("write"));
        Assert.That(json["usage"]!["total_tokens"]!.GetValue<int>(), Is.EqualTo(10));
    }

    [Test]
    public void Chunks_stream_role_then_indexed_tool_calls_then_the_finish()
    {
        var chunks = new ChatCompletionWriter().Chunks("chatcmpl-1", "m", WithCall).Select(chunk => JsonNode.Parse(chunk)!).ToArray();

        Assert.That(chunks, Has.Length.EqualTo(3));
        Assert.That(chunks[0]["choices"]![0]!["delta"]!["role"]!.GetValue<string>(), Is.EqualTo("assistant"));
        Assert.That(chunks[1]["choices"]![0]!["delta"]!["tool_calls"]![0]!["index"]!.GetValue<int>(), Is.Zero);
        Assert.That(chunks[2]["choices"]![0]!["finish_reason"]!.GetValue<string>(), Is.EqualTo("tool_calls"));
    }

    [Test]
    public void Models_lists_the_one_model_the_server_answers_to()
    {
        var models = JsonNode.Parse(new ChatCompletionWriter().Models("qwen"))!;

        Assert.That(models["data"]![0]!["id"]!.GetValue<string>(), Is.EqualTo("qwen"));
    }
}
