using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class ToolCallParserTests
{
    [Test]
    public void TryParse_readsTaggedJsonTheInstructModelEmits()
    {
        var parsed = new ToolCallParser().TryParse(
            "<tool_call>\n{\"name\": \"complete_task\", \"arguments\": {\"summary\": \"done\"}}\n</tool_call>",
            out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(call!.Name, Is.EqualTo("complete_task"));
            Assert.That(call.Arguments["summary"], Is.EqualTo("done"));
        });
    }

    [Test]
    public void TryParse_rejectsACopiedToolNameTemplate()
    {
        var parsed = new ToolCallParser().TryParse(
            "<tool_call>\n{\"name\": \"TOOL_NAME\", \"arguments\": {\"KEY\": \"VALUE\"}}\n</tool_call>",
            out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.False);
            Assert.That(call, Is.Null);
        });
    }

    [Test]
    public void TryParse_readsABareFunctionCallWhenTheModelSkipsTheTag()
    {
        var parsed = new ToolCallParser().TryParse("write_file(notes.txt)", out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(call!.Name, Is.EqualTo("write_file"));
            Assert.That(call.Arguments["path"], Is.EqualTo("notes.txt"));
        });
    }
}
