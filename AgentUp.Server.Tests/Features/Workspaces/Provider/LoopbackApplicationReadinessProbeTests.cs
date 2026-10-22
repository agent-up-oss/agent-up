using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.Workspaces.Providers;

namespace AgentUp.Server.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class LoopbackApplicationReadinessProbeTests
{
    [Test]
    public async Task TcpReady_isTrueWhenLoopbackAcceptsAndFalseWhenNothingListens()
    {
        using var probe = new LoopbackApplicationReadinessProbe();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.That(await probe.TcpReadyAsync(port, CancellationToken.None), Is.True);

        listener.Stop();
        Assert.That(await probe.TcpReadyAsync(port, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task HttpReady_isTrueForASuccessStatusOnLoopback()
    {
        using var probe = new LoopbackApplicationReadinessProbe();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var serverCts = new CancellationTokenSource();
        var server = ServeAsync(listener, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", serverCts.Token);

        try
        {
            Assert.That(await probe.HttpReadyAsync(port, "/health", CancellationToken.None), Is.True);
        }
        finally
        {
            await serverCts.CancelAsync();
            listener.Stop();
            try
            {
                await server;
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                Assert.That(ex, Is.Not.Null);
            }
        }
    }

    [Test]
    public async Task HttpReady_isFalseWhenNothingListens()
    {
        using var probe = new LoopbackApplicationReadinessProbe();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        Assert.That(await probe.HttpReadyAsync(port, "/health", CancellationToken.None), Is.False);
    }

    private static async Task ServeAsync(TcpListener listener, string response, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var buffer = new byte[1024];
        _ = await stream.ReadAsync(buffer, cancellationToken);
        var bytes = System.Text.Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken);
    }
}
