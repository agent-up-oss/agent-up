using System.Text;

namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditEventLogReverseReader
{
    private const int ChunkSize = 4 * 1024 * 1024;

    internal static async IAsyncEnumerable<string> ReadLinesReverseAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length == 0)
            yield break;

        var incompleteLeadingLine = string.Empty;
        var position = stream.Length;

        while (position > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readSize = (int)Math.Min(ChunkSize, position);
            position -= readSize;
            stream.Seek(position, SeekOrigin.Begin);

            var buffer = new byte[readSize];
            await stream.ReadExactlyAsync(buffer, cancellationToken);

            var chunk = Encoding.UTF8.GetString(buffer);
            var combined = string.Concat(chunk, incompleteLeadingLine);
            var lines = combined.Split('\n');
            incompleteLeadingLine = lines[0];

            for (var index = lines.Length - 1; index >= 1; index--)
            {
                var line = lines[index].TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(line))
                    yield return line;
            }
        }

        if (!string.IsNullOrWhiteSpace(incompleteLeadingLine))
            yield return incompleteLeadingLine.TrimEnd('\r');
    }
}
