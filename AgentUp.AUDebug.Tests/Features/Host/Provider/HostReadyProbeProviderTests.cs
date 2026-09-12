using System.Net;
using AgentUp.AUDebug.Features.Host.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class HostReadyProbeProviderTests
{
    [Test]
    public async Task WaitAsync_returnsWhenHttpResponds()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var handle = listener.GetContextAsync();
        _ = Task.Run(async () =>
        {
            var context = await handle;
            context.Response.StatusCode = 200;
            context.Response.Close();
        });

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        await new HostReadyProbeProvider(http).WaitAsync(prefix + "status", CancellationToken.None);
        Assert.Pass();
    }

    [Test]
    public async Task CheckAsync_returnsFalseWhenUnreachable()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };
        var ready = await new HostReadyProbeProvider(http).CheckAsync("http://127.0.0.1:1/", CancellationToken.None);
        Assert.That(ready, Is.False);
    }

    [Test]
    public void WaitAsync_alreadyCanceled_throws()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(50) };
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await new HostReadyProbeProvider(http).WaitAsync("http://127.0.0.1:1/", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task CheckAsync_returnsFalseWhenHttpClientTimesOut()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{GetFreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(80) };
            var ready = await new HostReadyProbeProvider(http).CheckAsync(prefix, CancellationToken.None);
            Assert.That(ready, Is.False);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static int GetFreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
