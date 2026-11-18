using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class ChatCompletionReaderTests
{
    [Test]
    public void Read_splits_a_tagged_tool_call_from_the_text()
    {
        var completion = new ChatCompletionReader().Read(
            "Writing it.\n<tool_call>\n{\"name\": \"write\", \"arguments\": {\"filePath\": \"a.txt\", \"content\": \"hi\"}}\n</tool_call>",
            ["write"], 10, 5);

        Assert.That(completion.Content, Is.EqualTo("Writing it."));
        Assert.That(completion.ToolCalls.Single().Name, Is.EqualTo("write"));
        Assert.That(completion.ToolCalls.Single().ArgumentsJson, Does.Contain("a.txt"));
        Assert.That(completion.FinishReason, Is.EqualTo("tool_calls"));
    }

    [Test]
    public void Read_keeps_a_call_to_a_tool_that_was_not_offered_as_text()
    {
        var completion = new ChatCompletionReader().Read(
            "<tool_call>{\"name\": \"rm_rf\", \"arguments\": {}}</tool_call>", ["write"], 1, 1);

        Assert.That(completion.ToolCalls, Is.Empty);
        Assert.That(completion.Content, Does.Contain("rm_rf"));
        Assert.That(completion.FinishReason, Is.EqualTo("stop"));
    }

    [Test]
    public void Read_ignores_a_tool_call_that_is_not_json()
    {
        var completion = new ChatCompletionReader().Read("<tool_call>{not json}</tool_call><|im_end|>", ["write"], 1, 1);

        Assert.That(completion.ToolCalls, Is.Empty);
        Assert.That(completion.Content, Is.EqualTo("<tool_call>{not json}</tool_call>"));
    }
}
