using System.Text;
using AgentUp.Server.Features.Processes.Models;
using AgentUp.Server.Features.Processes.Providers;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class ProcessOutputPumpTests
{
    [Test]
    public async Task DrainAsync_framesChunksThatStraddleAReadBoundary()
    {
        var payload = "hello\nworld\nfinal-no-nl";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(payload)), Encoding.UTF8);
        var lines = new List<(string Text, ProcessOutputStream Stream)>();

        await new ProcessOutputPump().DrainAsync(
            reader,
            ProcessOutputStream.Stdout,
            (line, stream, _) =>
            {
                lines.Add((line, stream));
                return Task.CompletedTask;
            },
            bufferChars: 4);

        Assert.That(lines.Select(line => line.Text), Is.EqualTo(new[] { "hello", "world", "final-no-nl" }));
        Assert.That(lines, Has.All.Matches<(string Text, ProcessOutputStream Stream)>(line => line.Stream == ProcessOutputStream.Stdout));
    }

    [Test]
    public async Task DrainAsync_keepsALineLongerThanTheReadBuffer()
    {
        var body = new string('L', 8_192);
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(body + "\n")), Encoding.UTF8);
        var lines = new List<string>();

        await new ProcessOutputPump().DrainAsync(
            reader,
            ProcessOutputStream.Stderr,
            (line, _, _) =>
            {
                lines.Add(line);
                return Task.CompletedTask;
            },
            bufferChars: 64);

        Assert.That(lines, Is.EqualTo(new[] { body }));
    }

    [Test]
    public async Task DrainAsync_defaultBufferFlushesAFinalLine()
    {
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("only-tail")), Encoding.UTF8);
        var lines = new List<string>();

        await new ProcessOutputPump().DrainAsync(
            reader,
            ProcessOutputStream.Stderr,
            (line, stream, _) =>
            {
                Assert.That(stream, Is.EqualTo(ProcessOutputStream.Stderr));
                lines.Add(line);
                return Task.CompletedTask;
            });

        Assert.That(lines, Is.EqualTo(new[] { "only-tail" }));
    }

    [Test]
    public async Task DrainAsync_treatsAZeroBufferAsOneCharacter()
    {
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("ab\n")), Encoding.UTF8);
        var lines = new List<string>();

        await new ProcessOutputPump().DrainAsync(
            reader,
            ProcessOutputStream.Stdout,
            (line, _, _) =>
            {
                lines.Add(line);
                return Task.CompletedTask;
            },
            bufferChars: 0);

        Assert.That(lines, Is.EqualTo(new[] { "ab" }));
    }

    [Test]
    public async Task DrainAsync_emitsNothingForAnEmptyStream()
    {
        using var reader = new StreamReader(new MemoryStream(), Encoding.UTF8);
        var calls = 0;

        await new ProcessOutputPump().DrainAsync(
            reader,
            ProcessOutputStream.Stdout,
            (_, _, _) =>
            {
                calls++;
                return Task.CompletedTask;
            });

        Assert.That(calls, Is.EqualTo(0));
    }
}
