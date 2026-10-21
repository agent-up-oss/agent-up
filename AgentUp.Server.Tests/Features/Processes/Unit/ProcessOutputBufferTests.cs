using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Tests.Features.Processes.Unit;

[TestFixture]
public sealed class ProcessOutputBufferTests
{
    [Test]
    public void Constructor_rejectsCapacityBelowTwo()
    {
        Assert.That(() => new ProcessOutputBuffer(1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Append_keepsLinesUntilCapacity()
    {
        var buffer = new ProcessOutputBuffer(4);

        buffer.Append(new ProcessOutputLine("one", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("two", ProcessOutputStream.Stderr));

        var lines = buffer.Snapshot();
        Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { "one", "two" }));
        Assert.That(lines[1].Stream, Is.EqualTo(ProcessOutputStream.Stderr));
        Assert.That(buffer.DroppedCount, Is.EqualTo(0));
    }

    [Test]
    public void Append_overflowInsertsExplicitTruncationMarker()
    {
        var buffer = new ProcessOutputBuffer(3);
        buffer.Append(new ProcessOutputLine("a", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("b", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("c", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("d", ProcessOutputStream.Stderr));

        var lines = buffer.Snapshot();
        Assert.That(lines[0].Text, Is.EqualTo(ProcessOutputBuffer.TruncationMarker(2)));
        Assert.That(lines.Select(line => line.Text).Skip(1), Is.EqualTo(new[] { "c", "d" }));
        Assert.That(lines[^1].Stream, Is.EqualTo(ProcessOutputStream.Stderr));
        Assert.That(ProcessOutputBuffer.IsTruncationMarker(lines[0].Text), Is.True);
    }

    [Test]
    public void Clear_removesLinesAndDroppedCount()
    {
        var buffer = new ProcessOutputBuffer(2);
        buffer.Append(new ProcessOutputLine("a", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("b", ProcessOutputStream.Stdout));
        buffer.Append(new ProcessOutputLine("c", ProcessOutputStream.Stdout));

        buffer.Clear();

        Assert.That(buffer.Snapshot(), Is.Empty);
        Assert.That(buffer.DroppedCount, Is.EqualTo(0));
    }
}
