using System.Threading.Channels;

namespace AgentUp.TestAgents.Tests.Support;

/// <summary>A stdin the test can keep writing after the agent has started reading.</summary>
internal sealed class LinePipe : TextReader
{
    private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>();

    public void WriteLine(string line) => _lines.Writer.TryWrite(line);

    public void Complete() => _lines.Writer.TryComplete();

    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (!await _lines.Reader.WaitToReadAsync(cancellationToken))
            return null;
        return _lines.Reader.TryRead(out var line) ? line : null;
    }
}
