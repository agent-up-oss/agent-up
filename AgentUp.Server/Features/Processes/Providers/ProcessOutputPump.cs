using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Features.Processes.Providers;

public sealed class ProcessOutputPump
{
    public const int DefaultReadBufferChars = 4096;

    public Task DrainAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        Func<string, ProcessOutputStream, CancellationToken, Task> onLine,
        CancellationToken cancellationToken = default)
        => DrainAsync(reader, stream, onLine, DefaultReadBufferChars, cancellationToken);

    public async Task DrainAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        Func<string, ProcessOutputStream, CancellationToken, Task> onLine,
        int bufferChars,
        CancellationToken cancellationToken = default)
    {
        var framer = new ProcessOutputFramer();
        var buffer = new char[Math.Max(1, bufferChars)];

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;

            foreach (var line in framer.Push(buffer.AsSpan(0, read)))
                await onLine(line, stream, cancellationToken);
        }

        var tail = framer.Flush();
        if (tail is not null)
            await onLine(tail, stream, cancellationToken);
    }
}
