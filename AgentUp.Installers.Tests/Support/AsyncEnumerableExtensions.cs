namespace AgentUp.Installers.Tests.Support;

public static class AsyncEnumerableExtensions
{
    public static async Task DrainAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var _ in source.WithCancellation(cancellationToken))
            count++;
        GC.KeepAlive(count);
    }
}
