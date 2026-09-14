using System.Text;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Reads agent CLI output as characters rather than lines.
/// <para>
/// Line reads deadlock on these CLIs: <c>claude setup-token</c> writes
/// <c>Paste code here: </c> with no trailing newline and then waits for stdin, so a
/// <c>ReadLineAsync</c> never returns and the sign-in link it printed just above is never
/// surfaced. This reader emits a segment on every newline and also flushes whatever is pending
/// once the CLI has gone quiet for <paramref name="idle"/>, which is what makes an unterminated
/// prompt observable.
/// </para>
/// </summary>
public sealed class AgentLoginOutputReader(TextReader reader, TimeSpan idle)
{
    private const int BufferSize = 1024;

    public async Task ReadAsync(Action<string> onSegment, CancellationToken cancellationToken)
    {
        var buffer = new char[BufferSize];
        var pending = new StringBuilder();
        var read = reader.ReadAsync(buffer, cancellationToken).AsTask();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completed = await Task.WhenAny(read, Task.Delay(idle, cancellationToken));
            if (completed != read)
            {
                // The delay won the race, so the CLI has gone quiet. Anything buffered is a
                // prompt it does not intend to terminate, and the caller needs to see it.
                cancellationToken.ThrowIfCancellationRequested();
                Flush(pending, onSegment);
                continue;
            }

            var count = await read;
            if (count == 0)
            {
                Flush(pending, onSegment);
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var character = buffer[index];
                if (character is '\n' or '\r')
                {
                    Flush(pending, onSegment);
                    continue;
                }

                pending.Append(character);
            }

            read = reader.ReadAsync(buffer, cancellationToken).AsTask();
        }
    }

    private static void Flush(StringBuilder pending, Action<string> onSegment)
    {
        if (pending.Length == 0)
            return;
        var segment = pending.ToString();
        pending.Clear();
        onSegment(segment);
    }
}
