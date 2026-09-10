using System.Text;

namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditEventOffsetLineReader
{
    internal static async Task<string?> ReadLineAsync(
        string path,
        long offset,
        int length,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || length <= 0 || offset < 0)
            return null;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (offset >= stream.Length)
            return null;

        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var chunk = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (chunk == 0)
                break;

            read += chunk;
        }

        if (read <= 0)
            return null;

        return Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\r', '\n');
    }

    internal static async Task<(long Offset, int Length, string Line)?> AppendLineAsync(
        string path,
        string line,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            options: FileOptions.Asynchronous);
        stream.Seek(0, SeekOrigin.End);
        var offset = stream.Position;
        var payload = Encoding.UTF8.GetBytes(line + Environment.NewLine);
        await stream.WriteAsync(payload, cancellationToken);
        return (offset, payload.Length - Environment.NewLine.Length, line);
    }

    internal static async IAsyncEnumerable<(long Offset, int Length, string Line)> ReadLinesForwardWithOffsetsAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            yield break;

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        long offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            var length = Encoding.UTF8.GetByteCount(line);
            yield return (offset, length, line);
            offset += length + Environment.NewLine.Length;
        }
    }
}
