using AgentUp.Server.Features.Processes.Providers;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class ProcessOutputFramerTests
{
    [Test]
    public void Push_carriesAPartialLineAcrossChunkBoundaries()
    {
        var framer = new ProcessOutputFramer();

        Assert.That(framer.Push("hel"), Is.Empty);
        Assert.That(framer.Push("lo\nwor"), Is.EqualTo(new[] { "hello" }));
        Assert.That(framer.Push("ld\n"), Is.EqualTo(new[] { "world" }));
        Assert.That(framer.Flush(), Is.Null);
    }

    [Test]
    public void Flush_emitsAFinalLineWithoutATrailingNewline()
    {
        var framer = new ProcessOutputFramer();

        Assert.That(framer.Push("final-no-nl"), Is.Empty);
        Assert.That(framer.Flush(), Is.EqualTo("final-no-nl"));
        Assert.That(framer.Flush(), Is.Null);
    }

    [Test]
    public void Push_keepsALineLongerThanTheReadBuffer()
    {
        var framer = new ProcessOutputFramer();
        var body = new string('L', 10_000);

        Assert.That(framer.Push(body[..4_000]), Is.Empty);
        Assert.That(framer.Push(body[4_000..] + "\nnext\n"), Is.EqualTo(new[] { body, "next" }));
    }

    [Test]
    public void Push_stripsCarriageReturnsAndKeepsBlankLines()
    {
        var framer = new ProcessOutputFramer();

        Assert.That(framer.Push("one\r\n\r\ntwo\n"), Is.EqualTo(new[] { "one", "", "two" }));
    }
}
