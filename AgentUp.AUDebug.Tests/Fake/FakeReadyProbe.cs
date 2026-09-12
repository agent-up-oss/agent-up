using AgentUp.AUDebug.Features.Host.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeReadyProbe : IHostReadyProbe
{
    public List<string> Urls { get; } = [];
    public Dictionary<string, bool> Ready { get; } = new(StringComparer.Ordinal);
    public bool DelayUntilCanceled { get; set; }

    public async Task WaitAsync(string url, CancellationToken cancellationToken)
    {
        Urls.Add(url);
        if (!DelayUntilCanceled)
            return;

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task<bool> CheckAsync(string url, CancellationToken cancellationToken)
    {
        Urls.Add(url);
        cancellationToken.ThrowIfCancellationRequested();
        if (DelayUntilCanceled)
            return Task.FromResult(false);
        return Task.FromResult(Ready.GetValueOrDefault(url, true));
    }
}
