using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

// This reader exists because line reads deadlock on agent CLIs that prompt without a newline.
// These tests pin that behaviour: newline-terminated output still arrives as whole lines, and an
// unterminated prompt arrives anyway once the CLI stops writing.
[TestFixture]
public sealed class AgentLoginOutputReaderTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(50);

    [Test]
    public async Task ReadAsync_emitsOneSegmentPerLine()
    {
        var segments = await ReadAsync("first\nsecond\nthird\n");

        Assert.That(segments, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public async Task ReadAsync_emitsAPromptThatNeverEndsItsLine()
    {
        var segments = await ReadAsync("Visit https://example.com/login\nPaste code here: ");

        Assert.Multiple(() =>
        {
            Assert.That(segments, Has.Count.EqualTo(2));
            Assert.That(segments[0], Is.EqualTo("Visit https://example.com/login"));
            Assert.That(segments[1], Is.EqualTo("Paste code here: "),
                "A blocking prompt has no trailing newline and must still be surfaced");
        });
    }

    [Test]
    public async Task ReadAsync_treatsCarriageReturnsAsSegmentBreaksAndSkipsEmptyOnes()
    {
        var segments = await ReadAsync("spinner\r\nprogress\r\r\ndone\n");

        Assert.That(segments, Is.EqualTo(new[] { "spinner", "progress", "done" }));
    }

    // The case that actually matters in production: the CLI is still running, so there is no
    // end-of-stream to flush on. The prompt has to surface purely because the CLI went quiet.
    [Test]
    public async Task ReadAsync_emitsAPendingPromptWhileTheProcessIsStillRunning()
    {
        var segments = new List<string>();
        var surfaced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new StallingReader("Paste code here: ");
        var reader = new AgentLoginOutputReader(source, Idle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var reading = reader.ReadAsync(
            segment =>
            {
                segments.Add(segment);
                surfaced.TrySetResult();
            },
            cancellation.Token);

        await surfaced.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();
        // The reader only ends by being cancelled: its source never closes, the way a CLI
        // waiting on stdin never does.
        Assert.CatchAsync<OperationCanceledException>(async () => await reading);

        Assert.That(segments, Is.EqualTo(new[] { "Paste code here: " }));
    }

    [Test]
    public void ReadAsync_stopsWhenCancelled()
    {
        using var source = new NeverEndingReader();
        var reader = new AgentLoginOutputReader(source, Idle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await reader.ReadAsync(_ => { }, cancellation.Token));
    }

    private static async Task<List<string>> ReadAsync(string output)
    {
        var segments = new List<string>();
        using var source = new StringReader(output);
        var reader = new AgentLoginOutputReader(source, Idle);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await reader.ReadAsync(segments.Add, cancellation.Token);
        return segments;
    }

    /// <summary>Writes one chunk, then blocks the way a CLI waiting on stdin does.</summary>
    private sealed class StallingReader(string chunk) : TextReader
    {
        private bool _written;

        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_written)
                return new ValueTask<int>(new TaskCompletionSource<int>().Task.WaitAsync(cancellationToken));
            _written = true;
            chunk.AsMemory().CopyTo(buffer);
            return new ValueTask<int>(chunk.Length);
        }
    }

    private sealed class NeverEndingReader : TextReader
    {
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) =>
            new(new TaskCompletionSource<int>().Task.WaitAsync(cancellationToken));
    }
}
