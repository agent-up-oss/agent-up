using AgentUp.TestAgents.Features.Inference.Interfaces;

namespace AgentUp.TestAgents.Tests.Support;

internal sealed class FakeHttpDownloader : IHttpDownloader
{
    public byte[] Bytes { get; init; } = [1, 2, 3, 4];
    public Exception? Failure { get; init; }
    public int Calls { get; private set; }

    public Task DownloadAsync(Uri source, string destination, CancellationToken cancellationToken)
    {
        Calls++;
        if (Failure is not null)
            return Task.FromException(Failure);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return File.WriteAllBytesAsync(destination, Bytes, cancellationToken);
    }
}
