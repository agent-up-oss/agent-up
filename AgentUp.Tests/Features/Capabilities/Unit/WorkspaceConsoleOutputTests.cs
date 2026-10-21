using System.Text.Json;
using AgentUp.Tests.Support;

namespace AgentUp.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class WorkspaceConsoleOutputTests
{
    [Test]
    public void FormatBlock_reads_text_from_stream_identity_objects()
    {
        using var document = JsonDocument.Parse(
            """[{"text":"listening","stream":"stdout"},{"text":"[err] boom","stream":"stderr"}]""");

        var block = WorkspaceConsoleOutput.FormatBlock("Example API", document.RootElement);

        Assert.That(block, Is.EqualTo(
            $"Example API console:{Environment.NewLine}listening{Environment.NewLine}[err] boom"));
    }

    [Test]
    public void FormatBlock_still_reads_legacy_string_arrays()
    {
        using var document = JsonDocument.Parse("""["first","second"]""");

        var block = WorkspaceConsoleOutput.FormatBlock("Docs", document.RootElement);

        Assert.That(block, Is.EqualTo($"Docs console:{Environment.NewLine}first{Environment.NewLine}second"));
    }

    [Test]
    public void FormatBlock_returns_empty_when_there_are_no_lines()
    {
        using var document = JsonDocument.Parse("[]");

        Assert.That(WorkspaceConsoleOutput.FormatBlock("Example Web", document.RootElement), Is.Empty);
    }
}
