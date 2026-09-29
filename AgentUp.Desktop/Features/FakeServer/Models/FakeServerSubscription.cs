namespace AgentUp.Desktop.Features.FakeServer.Models;

public sealed class FakeServerSubscription(Action dispose) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        dispose();
    }
}
