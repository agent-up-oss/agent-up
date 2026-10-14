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

    [Test]
    public void TryParse_rejectsWhitespace()
    {
        Assert.That(new ToolCallParser().TryParse("  \n", out var call), Is.False);
        Assert.That(call, Is.Null);
    }

    [Test]
    public void TryParse_readsBareNamedJson()
    {
        var parsed = new ToolCallParser().TryParse(
            """{"name": "read_file", "arguments": {"path": "notes.txt", "n": 1}}""",
            out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(call!.Name, Is.EqualTo("read_file"));
            Assert.That(call.Arguments["path"], Is.EqualTo("notes.txt"));
            Assert.That(call.Arguments["n"], Is.EqualTo("1"));
        });
    }

    [Test]
    public void TryParse_rejectsNamedJsonThatStillSaysToolName()
    {
        Assert.That(
            new ToolCallParser().TryParse("""{"name": "TOOL_NAME", "arguments": {}}""", out var call),
            Is.False);
        Assert.That(call, Is.Null);
    }

    [Test]
    public void TryParse_rejectsTaggedJsonMissingAName()
    {
        Assert.That(
            new ToolCallParser().TryParse("<tool_call>{\"arguments\": {}}</tool_call>", out var call),
            Is.False);
        Assert.That(call, Is.Null);
    }

    [Test]
    public void TryParse_rejectsBrokenTaggedJson()
    {
        Assert.That(
            new ToolCallParser().TryParse("<tool_call>{not json}</tool_call>", out var call),
            Is.False);
        Assert.That(call, Is.Null);
    }

    [Test]
    public void TryParse_rejectsNamedJsonWithBrokenArguments()
    {
        Assert.That(
            new ToolCallParser().TryParse("""{"name": "read_file", "arguments": {not json}""", out var call),
            Is.False);
        Assert.That(call, Is.Null);
    }

    [Test]
    public void TryParse_readsAnEmptyCallSite()
    {
        var parsed = new ToolCallParser().TryParse("list_files()", out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(call!.Name, Is.EqualTo("list_files"));
            Assert.That(call.Arguments, Is.Empty);
        });
    }

    [Test]
    public void TryParse_readsKeyValueCallSiteArguments()
    {
        var parsed = new ToolCallParser().TryParse("write_file(path=notes.txt)", out var call);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(call!.Arguments["path"], Is.EqualTo("notes.txt"));
        });
    }

    [Test]
    public void TryParse_readsQuotedPositionalArguments()
    {
        var parser = new ToolCallParser();

        Assert.Multiple(() =>
        {
            Assert.That(parser.TryParse("complete_task(\"all done\")", out var complete), Is.True);
            Assert.That(complete!.Arguments["summary"], Is.EqualTo("all done"));
            Assert.That(parser.TryParse("read_file('notes.txt')", out var read), Is.True);
            Assert.That(read!.Arguments["path"], Is.EqualTo("notes.txt"));
            Assert.That(parser.TryParse("list_files(.)", out var list), Is.True);
            Assert.That(list!.Arguments["path"], Is.EqualTo("."));
        });
    }
}
